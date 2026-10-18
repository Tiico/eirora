package com.eirora.health

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.metadata.Metadata
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject
import java.time.Duration
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicInteger
import kotlin.reflect.KClass

/**
 * What the game asks of Health Connect (ADR-0007). Each call answers exactly once through its [BridgeCallback].
 * Totals are computed here and only totals cross to C#; nothing here logs, stores or sends health values (HLT-10).
 */
object HealthBridge {
    const val PROVIDER_PACKAGE = "com.google.android.apps.healthdata"
    private const val MINIMUM_WORKOUT_MINUTES = 10L // D-42.

    internal const val EXTRA_REQUEST_ID = "eirora.requestId"
    internal const val EXTRA_PERMISSIONS = "eirora.permissions"

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val nextRequestId = AtomicInteger()
    private val pendingPermissionRequests = ConcurrentHashMap<Int, BridgeCallback>()

    private val permissions: Map<String, String> = mapOf(
        "steps" to HealthPermission.getReadPermission(StepsRecord::class),
        "exercise" to HealthPermission.getReadPermission(ExerciseSessionRecord::class),
        "sleep" to HealthPermission.getReadPermission(SleepSessionRecord::class),
    )

    /** Whether Health Connect is available and which of steps, exercise and sleep the player has granted. */
    @JvmStatic
    fun getStatus(context: Context, callback: BridgeCallback) = respond(callback) {
        val appContext = context.applicationContext
        val sdkStatus = HealthConnectClient.getSdkStatus(appContext, PROVIDER_PACKAGE)
        val json = JSONObject().put("sdkStatus", sdkStatusName(sdkStatus))
        if (sdkStatus == HealthConnectClient.SDK_AVAILABLE) {
            json.put("granted", JSONArray(grantedKeys(appContext)))
        }
        json.toString()
    }

    /**
     * Shows the Health Connect permission dialog for [keys] (comma separated: steps, exercise, sleep) and answers with
     * the keys granted afterwards. A denial is a normal answer, not an error.
     */
    @JvmStatic
    fun requestPermissions(activity: Activity, keys: String, callback: BridgeCallback) {
        val requested = try {
            keys.split(',').filter { it.isNotBlank() }.map { permissions[it.trim()] ?: throw IllegalArgumentException("Unknown key $it") }.toSet()
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
                completePermissionRequest(activity, requestId, e)
            }
        }
    }

    internal fun completePermissionRequest(context: Context, requestId: Int, error: Exception?) {
        val callback = pendingPermissionRequests.remove(requestId) ?: return
        respond(callback) {
            if (error != null) throw error
            JSONObject().put("granted", JSONArray(grantedKeys(context.applicationContext))).toString()
        }
    }

    /**
     * Health Connect's total of [type] (steps, exercise or sleep) between [startMs] and [endMs], and the part of it that
     * Health Connect marks as manually entered (D-68). Steps and sleep use Health Connect's own de-duplicating aggregate
     * (INV-B1, D-71); sleep is in minutes. Exercise is the number of sessions of at least ten minutes that start in the
     * interval, with overlapping sessions from different sources merged into one (D-42).
     */
    @JvmStatic
    fun readTotal(context: Context, type: String, startMs: Long, endMs: Long, callback: BridgeCallback) = respond(callback) {
        val client = HealthConnectClient.getOrCreate(context.applicationContext)
        val start = Instant.ofEpochMilli(startMs)
        val end = Instant.ofEpochMilli(endMs)
        val range = TimeRangeFilter.between(start, end)
        val (total, manual) = when (type) {
            "steps" -> {
                val total = client.aggregate(AggregateRequest(setOf(StepsRecord.COUNT_TOTAL), range))[StepsRecord.COUNT_TOTAL] ?: 0L
                val manual = readAll(client, StepsRecord::class, range).filter { isManual(it) && !it.startTime.isBefore(start) }.sumOf { it.count }
                total to manual
            }
            "sleep" -> {
                val total = client.aggregate(AggregateRequest(setOf(SleepSessionRecord.SLEEP_DURATION_TOTAL), range))[SleepSessionRecord.SLEEP_DURATION_TOTAL]
                val manual = readAll(client, SleepSessionRecord::class, range).filter { isManual(it) }
                    .sumOf { Duration.between(maxOf(it.startTime, start), minOf(it.endTime, end)).toMinutes().coerceAtLeast(0L) }
                (total?.toMinutes() ?: 0L) to manual
            }
            "exercise" -> workouts(readAll(client, ExerciseSessionRecord::class, range).filter { !it.startTime.isBefore(start) && it.startTime.isBefore(end) })
            else -> throw IllegalArgumentException("Unknown type $type")
        }
        JSONObject().put("total", total).put("manual", minOf(manual, total)).toString()
    }

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

    /** Sessions of at least ten minutes after merging overlaps, and how many of them were entered by hand only. */
    private fun workouts(sessions: List<ExerciseSessionRecord>): Pair<Long, Long> {
        var count = 0L
        var manual = 0L
        var groupStart: Instant? = null
        var groupEnd: Instant? = null
        var groupManual = true
        fun close() {
            val s = groupStart ?: return
            val e = groupEnd ?: return
            if (Duration.between(s, e).toMinutes() >= MINIMUM_WORKOUT_MINUTES) {
                count++
                if (groupManual) manual++
            }
        }
        for (session in sessions.sortedBy { it.startTime }) {
            if (groupEnd != null && !session.startTime.isAfter(groupEnd)) {
                if (session.endTime.isAfter(groupEnd)) groupEnd = session.endTime
                groupManual = groupManual && isManual(session)
            } else {
                close()
                groupStart = session.startTime
                groupEnd = session.endTime
                groupManual = isManual(session)
            }
        }
        close()
        return count to manual
    }

    private suspend fun <T : Record> readAll(client: HealthConnectClient, type: KClass<T>, range: TimeRangeFilter): List<T> {
        val records = mutableListOf<T>()
        var pageToken: String? = null
        do {
            val response = client.readRecords(ReadRecordsRequest(type, range, pageSize = 1000, pageToken = pageToken))
            records += response.records
            pageToken = response.pageToken
        } while (pageToken != null)
        return records
    }

    private fun isManual(record: Record) = record.metadata.recordingMethod == Metadata.RECORDING_METHOD_MANUAL_ENTRY

    private suspend fun grantedKeys(context: Context): List<String> {
        val granted = HealthConnectClient.getOrCreate(context).permissionController.getGrantedPermissions()
        return permissions.filterValues { it in granted }.keys.sorted()
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

    private fun tryStart(activity: Activity, intent: Intent): Boolean = try {
        activity.startActivity(intent)
        true
    } catch (e: ActivityNotFoundException) {
        false
    }

    private fun sdkStatusName(status: Int): String = when (status) {
        HealthConnectClient.SDK_AVAILABLE -> "available"
        HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED -> "update_required"
        else -> "unavailable"
    }
}
