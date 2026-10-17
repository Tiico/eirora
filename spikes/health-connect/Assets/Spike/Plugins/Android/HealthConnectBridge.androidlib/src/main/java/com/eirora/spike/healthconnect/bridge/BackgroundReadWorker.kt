package com.eirora.spike.healthconnect.bridge

import android.app.ActivityManager
import android.content.Context
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import java.time.Duration
import java.time.Instant

/** Tries to read the last day's steps from WorkManager and records only whether it worked. */
class BackgroundReadWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val now = Instant.now()
        val importance = processImportance()
        val run = try {
            val client = HealthConnectClient.getOrCreate(applicationContext)
            val response = client.readRecords(ReadRecordsRequest(StepsRecord::class, TimeRangeFilter.between(now.minus(Duration.ofDays(1)), now)))
            BackgroundRun(now, "ok", response.records.size, importance)
        } catch (e: SecurityException) {
            BackgroundRun(now, "permission_denied", -1, importance)
        } catch (e: Exception) {
            BackgroundRun(now, "error:${e.javaClass.simpleName}", -1, importance)
        }
        BackgroundRunLog.append(applicationContext, run)
        return Result.success()
    }

    private fun processImportance(): Int {
        val info = ActivityManager.RunningAppProcessInfo()
        ActivityManager.getMyMemoryState(info)
        return info.importance
    }
}
