package com.eirora.spike.healthconnect.bridge

import androidx.health.connect.client.records.ExerciseSessionRecord
import androidx.health.connect.client.records.Record
import androidx.health.connect.client.records.SleepSessionRecord
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.records.metadata.Device
import androidx.health.connect.client.records.metadata.Metadata
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.ZoneOffset

/** Serialises the record fields the spike shows. The field names are the contract with HealthRecord in C#. */
internal object RecordJson {
    fun toJson(record: Record): JSONObject {
        val json = JSONObject()
        when (record) {
            is StepsRecord -> json
                .put("type", "steps")
                .interval(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
                .put("count", record.count)

            is ExerciseSessionRecord -> json
                .put("type", "exercise")
                .interval(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
                .put("exerciseType", record.exerciseType)
                .put("exerciseTypeName", ConstantNames.of(ExerciseSessionRecord::class.java, "EXERCISE_TYPE_", record.exerciseType))
                .put("title", record.title ?: "")
                .put("segmentCount", record.segments.size)
                .put("lapCount", record.laps.size)

            is SleepSessionRecord -> json
                .put("type", "sleep")
                .interval(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
                .put("title", record.title ?: "")
                .put("stages", JSONArray(record.stages.map { stage ->
                    JSONObject()
                        .put("stage", stage.stage)
                        .put("stageName", ConstantNames.of(SleepSessionRecord::class.java, "STAGE_TYPE_", stage.stage))
                        .put("startMs", stage.startTime.toEpochMilli())
                        .put("endMs", stage.endTime.toEpochMilli())
                }))

            else -> throw IllegalArgumentException("Unsupported record type ${record.javaClass.simpleName}")
        }
        return json.metadata(record.metadata)
    }

    private fun JSONObject.interval(start: Instant, startOffset: ZoneOffset?, end: Instant, endOffset: ZoneOffset?): JSONObject = this
        .put("startMs", start.toEpochMilli())
        .put("endMs", end.toEpochMilli())
        .put("hasStartOffset", startOffset != null)
        .put("startOffsetSec", startOffset?.totalSeconds ?: 0)
        .put("hasEndOffset", endOffset != null)
        .put("endOffsetSec", endOffset?.totalSeconds ?: 0)

    private fun JSONObject.metadata(metadata: Metadata): JSONObject {
        put("id", metadata.id)
        put("origin", metadata.dataOrigin.packageName)
        put("recordingMethod", metadata.recordingMethod)
        put("recordingMethodName", ConstantNames.of(Metadata::class.java, "RECORDING_METHOD_", metadata.recordingMethod))
        put("lastModifiedMs", metadata.lastModifiedTime.toEpochMilli())
        put("clientRecordId", metadata.clientRecordId ?: "")
        put("clientRecordVersion", metadata.clientRecordVersion)
        val device = metadata.device
        put("hasDevice", device != null)
        if (device != null) {
            put("deviceType", device.type)
            put("deviceTypeName", ConstantNames.of(Device::class.java, "TYPE_", device.type))
            put("deviceManufacturer", device.manufacturer ?: "")
            put("deviceModel", device.model ?: "")
        }
        return this
    }
}
