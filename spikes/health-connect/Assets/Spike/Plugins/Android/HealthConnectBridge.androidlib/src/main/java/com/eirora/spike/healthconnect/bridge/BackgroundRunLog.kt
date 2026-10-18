package com.eirora.spike.healthconnect.bridge

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant

/**
 * The outcome of one background read: when, whether it worked, how many step records it saw and whether the app
 * was in the foreground (ActivityManager importance). Never a health value.
 */
internal data class BackgroundRun(val at: Instant, val outcome: String, val recordCount: Int, val importance: Int)

/** The last background runs, in app-private preferences so they survive the app being closed. */
internal object BackgroundRunLog {
    private const val PREFERENCES = "eirora_hc_spike_background"
    private const val RUNS = "runs"
    private const val MAX_RUNS = 100

    @Synchronized
    fun append(context: Context, run: BackgroundRun) {
        val runs = read(context)
        runs.put(JSONObject()
            .put("atMs", run.at.toEpochMilli())
            .put("outcome", run.outcome)
            .put("recordCount", run.recordCount)
            .put("importance", run.importance))
        val kept = JSONArray()
        for (i in maxOf(0, runs.length() - MAX_RUNS) until runs.length()) kept.put(runs.get(i))
        preferences(context).edit().putString(RUNS, kept.toString()).apply()
    }

    @Synchronized
    fun read(context: Context): JSONArray = JSONArray(preferences(context).getString(RUNS, "[]"))

    @Synchronized
    fun clear(context: Context) = preferences(context).edit().remove(RUNS).apply()

    private fun preferences(context: Context) = context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
}
