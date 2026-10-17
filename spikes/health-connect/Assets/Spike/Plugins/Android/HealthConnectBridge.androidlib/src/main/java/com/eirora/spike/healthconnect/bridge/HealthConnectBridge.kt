package com.eirora.spike.healthconnect.bridge

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.HealthConnectFeatures
import androidx.health.connect.client.changes.DeletionChange
import androidx.health.connect.client.changes.UpsertionChange
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateGroupByPeriodRequest
import androidx.health.connect.client.request.ChangesTokenRequest
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.LocalDateTime
import java.time.Period
import java.time.ZoneId
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlin.reflect.KClass

/**
 * Everything the C# side can ask of Health Connect. Each call answers exactly once through its [BridgeCallback].
 * Nothing here logs, stores or sends health values; the only things stored are an opaque change token and
 * the outcome of background runs (see [BackgroundRunLog]).
 */
object HealthConnectBridge {
    const val PROVIDER_PACKAGE = "com.google.android.apps.healthdata"
    private const val MAX_RECORDS = 20_000
    private const val PREFERENCES = "eirora_hc_spike"
    private const val CHANGES_TOKEN = "changes_token"
    private const val PERIODIC_WORK = "eirora-hc-spike-background-periodic"
    private const val ONE_TIME_WORK = "eirora-hc-spike-background-once"

    internal const val EXTRA_REQUEST_ID = "eirora.requestId"
    internal const val EXTRA_PERMISSIONS = "eirora.permissions"

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val nextRequestId = AtomicInteger()
    private val pendingPermissionRequests = ConcurrentHashMap<Int, BridgeCallback>()

    private val recordTypes: Map<String, KClass<out Record>> = mapOf(
        "steps" to StepsRecord::class,
        "exercise" to ExerciseSessionRecord::class,
        "sleep" to SleepSessionRecord::class,
    )

    private val permissions: Map<String, String> = mapOf(
        "steps" to HealthPermission.getReadPermission(StepsRecord::class),
        "exercise" to HealthPermission.getReadPermission(ExerciseSessionRecord::class),
        "sleep" to HealthPermission.getReadPermission(SleepSessionRecord::class),
        "background" to HealthPermission.PERMISSION_READ_HEALTH_DATA_IN_BACKGROUND,
        "history" to HealthPermission.PERMISSION_READ_HEALTH_DATA_HISTORY,
    )

    /** Availability, granted permissions and optional features. Works without any permission. */
    @JvmStatic
    fun getStatus(context: Context, callback: BridgeCallback) = respond(callback) {
        val appContext = context.applicationContext
        val sdkStatus = HealthConnectClient.getSdkStatus(appContext, PROVIDER_PACKAGE)
        val json = JSONObject()
            .put("sdkStatus", sdkStatusName(sdkStatus))
            .put("androidSdk", Build.VERSION.SDK_INT)
            .put("androidRelease", Build.VERSION.RELEASE)
            .put("deviceModel", "${Build.MANUFACTURER} ${Build.MODEL}")
            .put("providerVersions", JSONArray(providerVersions(appContext)))
        if (sdkStatus == HealthConnectClient.SDK_AVAILABLE) {
            val client = HealthConnectClient.getOrCreate(appContext)
            json.put("granted", JSONArray(client.permissionController.getGrantedPermissions().sorted()))
            json.put("backgroundFeature", featureStatusName(client.features.getFeatureStatus(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_IN_BACKGROUND)))
            json.put("historyFeature", featureStatusName(client.features.getFeatureStatus(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_HISTORY)))
        }
        json.toString()
    }

    /**
     * Shows the Health Connect permission dialog for [keys] (comma separated: steps, exercise, sleep, background, history).
     * Answers with the permissions granted after the dialog closes; a denial is a normal answer, not an error.
     */
    @JvmStatic
    fun requestPermissions(activity: Activity, keys: String, callback: BridgeCallback) {
        val requested = try {
            permissionsFor(keys)
        } catch (e: IllegalArgumentException) {
            callback.onError("invalid_argument", e.message ?: "")
            return
        }
        if (HealthConnectClient.getSdkStatus(activity, PROVIDER_PACKAGE) != HealthConnectClient.SDK_AVAILABLE) {
            callback.onError("unavailable", "Health Connect is not available")
            return
        }
        val requestId = nextRequestId.incrementAndGet()
        pendingPermissionRequests[requestId] = callback
        val intent = Intent(activity, HealthPermissionActivity::class.java)
            .putExtra(EXTRA_REQUEST_ID, requestId)
            .putExtra(EXTRA_PERMISSIONS, requested.toTypedArray())
        activity.runOnUiThread {
            try {
                activity.startActivity(intent)
            } catch (e: ActivityNotFoundException) {
                completePermissionRequest(activity, requestId, requested, null, e)
            }
        }
    }

    internal fun completePermissionRequest(context: Context, requestId: Int, requested: Set<String>, grantedNow: Set<String>?, error: Exception?) {
        val callback = pendingPermissionRequests.remove(requestId) ?: return
        respond(callback) {
            if (error != null) throw error
            val granted = HealthConnectClient.getOrCreate(context.applicationContext).permissionController.getGrantedPermissions()
            JSONObject()
                .put("requested", JSONArray(requested.sorted()))
                .put("grantedNow", JSONArray((grantedNow ?: emptySet()).sorted()))
                .put("granted", JSONArray(granted.sorted()))
                .toString()
        }
    }

    /** All records of [type] (steps, exercise or sleep) that overlap the interval, newest first. */
    @JvmStatic
    fun readRecords(context: Context, type: String, startMs: Long, endMs: Long, callback: BridgeCallback) = respond(callback) {
        val client = HealthConnectClient.getOrCreate(context.applicationContext)
        val recordType = recordTypeFor(type)
        val filter = TimeRangeFilter.between(Instant.ofEpochMilli(startMs), Instant.ofEpochMilli(endMs))
        val records = JSONArray()
        var pageToken: String? = null
        do {
            val response = client.readRecords(ReadRecordsRequest(recordType, filter, ascendingOrder = false, pageSize = 1000, pageToken = pageToken))
            response.records.forEach { records.put(RecordJson.toJson(it)) }
            pageToken = response.pageToken
        } while (pageToken != null && records.length() < MAX_RECORDS)
        JSONObject().put("records", records).put("truncated", pageToken != null).toString()
    }

    /**
     * Health Connect's own deduplicated total of [type] per local calendar day.
     * Steps are a count; exercise and sleep are durations in seconds.
     */
    @JvmStatic
    fun aggregateByDay(context: Context, type: String, startMs: Long, endMs: Long, callback: BridgeCallback) = respond(callback) {
        val client = HealthConnectClient.getOrCreate(context.applicationContext)
        val zone = ZoneId.systemDefault()
        val start = LocalDateTime.ofInstant(Instant.ofEpochMilli(startMs), zone).toLocalDate().atStartOfDay()
        val end = LocalDateTime.ofInstant(Instant.ofEpochMilli(endMs), zone)
        val metric = when (type) {
            "steps" -> StepsRecord.COUNT_TOTAL
            "exercise" -> ExerciseSessionRecord.EXERCISE_DURATION_TOTAL
            "sleep" -> SleepSessionRecord.SLEEP_DURATION_TOTAL
            else -> throw IllegalArgumentException("Unknown type $type")
        }
        val groups = client.aggregateGroupByPeriod(AggregateGroupByPeriodRequest(setOf(metric), TimeRangeFilter.between(start, end), Period.ofDays(1)))
        val days = JSONArray()
        for (group in groups) {
            val value: Long? = when (type) {
                "steps" -> group.result[StepsRecord.COUNT_TOTAL]
                "exercise" -> group.result[ExerciseSessionRecord.EXERCISE_DURATION_TOTAL]?.seconds
                else -> group.result[SleepSessionRecord.SLEEP_DURATION_TOTAL]?.seconds
            }
            days.put(JSONObject()
                .put("day", group.startTime.toLocalDate().toString())
                .put("hasValue", value != null)
                .put("value", value ?: 0L)
                .put("origins", JSONArray(group.result.dataOrigins.map { it.packageName }.sorted())))
        }
        JSONObject().put("days", days).toString()
    }

    /** Starts tracking changes (new, edited and deleted records) of [keys] from now on. */
    @JvmStatic
    fun startChangeTracking(context: Context, keys: String, callback: BridgeCallback) = respond(callback) {
        val types = keys.split(',').filter { it.isNotBlank() }.map { recordTypeFor(it.trim()) }.toSet()
        val token = HealthConnectClient.getOrCreate(context.applicationContext).getChangesToken(ChangesTokenRequest(types))
        preferences(context).edit().putString(CHANGES_TOKEN, token).apply()
        JSONObject().put("tracking", true).toString()
    }

    /** Every change since [startChangeTracking] or the previous call. */
    @JvmStatic
    fun readChanges(context: Context, callback: BridgeCallback) = respond(callback) {
        val preferences = preferences(context)
        var token = preferences.getString(CHANGES_TOKEN, null) ?: throw IllegalStateException("Change tracking is not started")
        val client = HealthConnectClient.getOrCreate(context.applicationContext)
        val upserts = JSONArray()
        val deletions = JSONArray()
        do {
            val response = client.getChanges(token)
            if (response.changesTokenExpired) {
                preferences.edit().remove(CHANGES_TOKEN).apply()
                return@respond JSONObject().put("expired", true).put("upserts", upserts).put("deletions", deletions).toString()
            }
            for (change in response.changes) {
                when (change) {
                    is UpsertionChange -> upserts.put(RecordJson.toJson(change.record))
                    is DeletionChange -> deletions.put(change.recordId)
                }
            }
            token = response.nextChangesToken
        } while (response.hasMore)
        preferences.edit().putString(CHANGES_TOKEN, token).apply()
        JSONObject().put("expired", false).put("upserts", upserts).put("deletions", deletions).toString()
    }

    /** Reads the last day's steps every 15 minutes (WorkManager's minimum) whether or not the app is open. */
    @JvmStatic
    fun scheduleBackgroundRead(context: Context) {
        val request = PeriodicWorkRequestBuilder<BackgroundReadWorker>(15, TimeUnit.MINUTES).build()
        WorkManager.getInstance(context.applicationContext).enqueueUniquePeriodicWork(PERIODIC_WORK, ExistingPeriodicWorkPolicy.KEEP, request)
    }

    /** One background read after [delaySeconds], so the app can be closed before it runs. */
    @JvmStatic
    fun runBackgroundReadOnce(context: Context, delaySeconds: Long) {
        val request = OneTimeWorkRequestBuilder<BackgroundReadWorker>().setInitialDelay(delaySeconds, TimeUnit.SECONDS).build()
        WorkManager.getInstance(context.applicationContext).enqueueUniqueWork(ONE_TIME_WORK, ExistingWorkPolicy.REPLACE, request)
    }

    @JvmStatic
    fun cancelBackgroundRead(context: Context) {
        val workManager = WorkManager.getInstance(context.applicationContext)
        workManager.cancelUniqueWork(PERIODIC_WORK)
        workManager.cancelUniqueWork(ONE_TIME_WORK)
    }

    @JvmStatic
    fun getBackgroundRuns(context: Context, callback: BridgeCallback) = respond(callback) {
        JSONObject().put("runs", BackgroundRunLog.read(context)).toString()
    }

    @JvmStatic
    fun clearBackgroundRuns(context: Context) = BackgroundRunLog.clear(context)

    /** Opens this app's page in Health Connect, where a denied permission can be granted again. */
    @JvmStatic
    fun openHealthConnectSettings(activity: Activity): Boolean {
        val candidates = mutableListOf<Intent>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            candidates += Intent("android.health.connect.action.MANAGE_HEALTH_PERMISSIONS").putExtra(Intent.EXTRA_PACKAGE_NAME, activity.packageName)
        }
        candidates += Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)
        return candidates.any { tryStart(activity, it) }
    }

    /** Opens the Play Store page that installs or updates Health Connect on Android 13 and lower. */
    @JvmStatic
    fun openProviderInstall(activity: Activity): Boolean {
        val uri = Uri.parse("market://details?id=$PROVIDER_PACKAGE&url=healthconnect%3A%2F%2Fonboarding")
        val intent = Intent(Intent.ACTION_VIEW, uri).setPackage("com.android.vending").putExtra("overlay", true).putExtra("callerId", activity.packageName)
        return tryStart(activity, intent)
    }

    /** The app label of a data origin, or its package name when the app is not visible to us. */
    @JvmStatic
    fun appLabel(context: Context, packageName: String): String = try {
        val packageManager = context.packageManager
        packageManager.getApplicationLabel(packageManager.getApplicationInfo(packageName, 0)).toString()
    } catch (e: Exception) {
        packageName
    }

    private fun respond(callback: BridgeCallback, block: suspend () -> String) {
        scope.launch {
            val result = try {
                Result.success(block())
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Result.failure(e)
            }
            // Exception messages from Health Connect name permissions and states, never record values.
            result.fold(
                onSuccess = { callback.onSuccess(it) },
                onFailure = { callback.onError(errorCode(it), "${it.javaClass.simpleName}: ${it.message ?: ""}") },
            )
        }
    }

    private fun errorCode(error: Throwable): String = when (error) {
        is SecurityException -> "permission_denied"
        is IllegalArgumentException -> "invalid_argument"
        is IllegalStateException -> "unavailable"
        is android.os.RemoteException -> "remote"
        is java.io.IOException -> "io"
        else -> "error"
    }

    private fun permissionsFor(keys: String): Set<String> =
        keys.split(',').filter { it.isNotBlank() }.map { key -> permissions[key.trim()] ?: throw IllegalArgumentException("Unknown permission $key") }.toSet()

    private fun recordTypeFor(type: String): KClass<out Record> = recordTypes[type] ?: throw IllegalArgumentException("Unknown type $type")

    private fun preferences(context: Context) = context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    private fun tryStart(activity: Activity, intent: Intent): Boolean = try {
        activity.startActivity(intent)
        true
    } catch (e: ActivityNotFoundException) {
        false
    }

    private fun providerVersions(context: Context): List<String> =
        listOf(PROVIDER_PACKAGE, "com.google.android.healthconnect.controller", "com.android.healthconnect.controller").mapNotNull { packageName ->
            try {
                "$packageName ${context.packageManager.getPackageInfo(packageName, 0).versionName}"
            } catch (e: Exception) {
                null
            }
        }

    private fun sdkStatusName(status: Int): String = when (status) {
        HealthConnectClient.SDK_AVAILABLE -> "SDK_AVAILABLE"
        HealthConnectClient.SDK_UNAVAILABLE -> "SDK_UNAVAILABLE"
        HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> "SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED"
        else -> status.toString()
    }

    private fun featureStatusName(status: Int): String = when (status) {
        HealthConnectFeatures.FEATURE_STATUS_AVAILABLE -> "AVAILABLE"
        HealthConnectFeatures.FEATURE_STATUS_UNAVAILABLE -> "UNAVAILABLE"
        else -> status.toString()
    }
}
