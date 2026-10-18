package com.eirora.spike.seeder

import android.os.Bundle
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.PermissionController
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.metadata.Device
import androidx.health.connect.client.records.metadata.Metadata
import androidx.health.connect.client.time.TimeRangeFilter
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.ZonedDateTime

/**
 * Writes, edits or deletes a fixed set of test records, chosen by the intent extra "action":
 * seed (default), edit, delete or clear. Only for emulators: never install it on a phone with real health data.
 *
 * adb shell am start -n com.eirora.spike.seeder.watch/com.eirora.spike.seeder.SeedActivity --es action seed
 */
class SeedActivity : ComponentActivity() {
    private val permissions = setOf(
        HealthPermission.getWritePermission(StepsRecord::class),
        HealthPermission.getWritePermission(ExerciseSessionRecord::class),
        HealthPermission.getWritePermission(SleepSessionRecord::class),
    )

    private val requestPermissions = registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
        if (granted.containsAll(permissions)) run() else show("Permissions denied: ${permissions - granted}")
    }

    private val isWatch get() = packageName.endsWith(".watch")
    private val zone = ZoneId.systemDefault()
    private val today get() = LocalDate.now(zone)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(TextView(this).apply { setPadding(48, 48, 48, 48) })
        lifecycleScope.launch {
            val granted = HealthConnectClient.getOrCreate(this@SeedActivity).permissionController.getGrantedPermissions()
            if (granted.containsAll(permissions)) run() else requestPermissions.launch(permissions)
        }
    }

    private fun run() {
        val action = intent.getStringExtra("action") ?: "seed"
        lifecycleScope.launch {
            val client = HealthConnectClient.getOrCreate(this@SeedActivity)
            val result = try {
                when (action) {
                    "seed" -> {
                        val records = if (isWatch) watchRecords() else phoneRecords()
                        client.insertRecords(records)
                        "seeded ${records.size} records"
                    }
                    "edit" -> {
                        client.insertRecords(listOf(run(ZonedDateTime.of(today.minusDays(1), LocalTime.of(17, 30), zone), 50, version = 2)))
                        "edited run"
                    }
                    "delete" -> {
                        client.deleteRecords(ExerciseSessionRecord::class, emptyList(), listOf("yoga-1"))
                        "deleted yoga"
                    }
                    "clear" -> {
                        val everything = TimeRangeFilter.after(Instant.EPOCH)
                        client.deleteRecords(StepsRecord::class, everything)
                        client.deleteRecords(ExerciseSessionRecord::class, everything)
                        client.deleteRecords(SleepSessionRecord::class, everything)
                        "cleared"
                    }
                    else -> "unknown action $action"
                }
            } catch (e: Exception) {
                "failed: ${e.javaClass.simpleName}: ${e.message}"
            }
            show("$packageName $action: $result")
        }
    }

    private fun show(text: String) {
        // The seeder's own made-up data; logging it is fine and lets the verification script wait for it.
        android.util.Log.i("EiroraSeeder", text)
        (window.decorView.findViewById<android.view.ViewGroup>(android.R.id.content).getChildAt(0) as TextView).text = text
    }

    private fun watchRecords(): List<Record> = buildList {
        val watch = Device(type = Device.TYPE_WATCH, manufacturer = "Seeder", model = "Watch")
        for (day in 0L..3L) {
            for (hour in 8..19) {
                val start = ZonedDateTime.of(today.minusDays(day), LocalTime.of(hour, 0), zone)
                if (start.plusHours(1).toInstant() > Instant.now()) break
                add(steps(start, start.plusHours(1), watchSteps(day, hour), Metadata.autoRecorded(watch, "steps-$day-$hour")))
            }
        }
        for (daysAgo in listOf(45L, 200L)) {
            val start = ZonedDateTime.of(today.minusDays(daysAgo), LocalTime.NOON, zone)
            add(steps(start, start.plusHours(1), 4321, Metadata.autoRecorded(watch, "steps-old-$daysAgo")))
        }
        add(run(ZonedDateTime.of(today.minusDays(1), LocalTime.of(17, 30), zone), 42, version = 1))
        val bedtime = ZonedDateTime.of(today.minusDays(1), LocalTime.of(23, 10), zone)
        add(sleep(bedtime, bedtime.plusMinutes(450), Metadata.autoRecorded(watch, "sleep-1")))
    }

    private fun phoneRecords(): List<Record> = buildList {
        val phone = Device(type = Device.TYPE_PHONE, manufacturer = "Seeder", model = "Phone")
        for (day in 0L..3L) {
            for (hour in 8..19) {
                val start = ZonedDateTime.of(today.minusDays(day), LocalTime.of(hour, 10), zone)
                if (start.plusMinutes(40).toInstant() > Instant.now()) break
                add(steps(start, start.plusMinutes(40), watchSteps(day, hour) * 8 / 10, Metadata.autoRecorded(phone, "steps-$day-$hour")))
            }
        }
        val manual = ZonedDateTime.of(today.minusDays(2), LocalTime.of(21, 0), zone)
        add(steps(manual, manual.plusHours(1), 1500, Metadata.manualEntry(clientRecordId = "steps-manual")))
        val run = ZonedDateTime.of(today.minusDays(1), LocalTime.of(17, 31), zone)
        add(exercise(run, run.plusMinutes(40), ExerciseSessionRecord.EXERCISE_TYPE_RUNNING, Metadata.autoRecorded(phone, "run-1")))
        val yoga = ZonedDateTime.of(today.minusDays(3), LocalTime.of(7, 0), zone)
        add(exercise(yoga, yoga.plusMinutes(30), ExerciseSessionRecord.EXERCISE_TYPE_YOGA, Metadata.manualEntry(clientRecordId = "yoga-1"), "Morgonyoga"))
        val bedtime = ZonedDateTime.of(today.minusDays(1), LocalTime.of(23, 30), zone)
        add(sleep(bedtime, bedtime.plusMinutes(410), Metadata.unknownRecordingMethod(clientRecordId = "sleep-1")))
    }

    private fun run(start: ZonedDateTime, minutes: Long, version: Long): ExerciseSessionRecord {
        val watch = Device(type = Device.TYPE_WATCH, manufacturer = "Seeder", model = "Watch")
        return exercise(start, start.plusMinutes(minutes), ExerciseSessionRecord.EXERCISE_TYPE_RUNNING, Metadata.activelyRecorded(watch, "run-1", version))
    }

    private fun watchSteps(day: Long, hour: Int): Long = 300L + ((day * 37 + hour * 53) % 700)

    private fun steps(start: ZonedDateTime, end: ZonedDateTime, count: Long, metadata: Metadata) =
        StepsRecord(startTime = start.toInstant(), startZoneOffset = start.offset, endTime = end.toInstant(), endZoneOffset = end.offset, count = count, metadata = metadata)

    private fun exercise(start: ZonedDateTime, end: ZonedDateTime, type: Int, metadata: Metadata, title: String? = null) =
        ExerciseSessionRecord(startTime = start.toInstant(), startZoneOffset = start.offset, endTime = end.toInstant(), endZoneOffset = end.offset, metadata = metadata, exerciseType = type, title = title)

    private fun sleep(start: ZonedDateTime, end: ZonedDateTime, metadata: Metadata): SleepSessionRecord {
        val light = start.plusMinutes(20)
        val deep = light.plusMinutes(150)
        val rem = deep.plusMinutes(90)
        val stages = listOf(
            SleepSessionRecord.Stage(start.toInstant(), light.toInstant(), SleepSessionRecord.STAGE_TYPE_AWAKE),
            SleepSessionRecord.Stage(light.toInstant(), deep.toInstant(), SleepSessionRecord.STAGE_TYPE_LIGHT),
            SleepSessionRecord.Stage(deep.toInstant(), rem.toInstant(), SleepSessionRecord.STAGE_TYPE_DEEP),
            SleepSessionRecord.Stage(rem.toInstant(), end.toInstant(), SleepSessionRecord.STAGE_TYPE_REM),
        )
        return SleepSessionRecord(startTime = start.toInstant(), startZoneOffset = start.offset, endTime = end.toInstant(), endZoneOffset = end.offset, metadata = metadata, stages = stages)
    }
}
