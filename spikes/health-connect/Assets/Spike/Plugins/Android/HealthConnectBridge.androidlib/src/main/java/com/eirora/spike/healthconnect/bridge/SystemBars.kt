package com.eirora.spike.healthconnect.bridge

import android.app.Activity
import androidx.core.view.WindowCompat

/** Paints the status and navigation bars in the spike's background colour with light icons. */
object SystemBars {
    @JvmStatic
    @Suppress("DEPRECATION") // The bar colours still apply below Android 15, which most test phones run.
    fun tint(activity: Activity, argb: Int) = activity.runOnUiThread {
        val window = activity.window
        window.statusBarColor = argb
        window.navigationBarColor = argb
        WindowCompat.getInsetsController(window, window.decorView).apply {
            isAppearanceLightStatusBars = false
            isAppearanceLightNavigationBars = false
        }
    }
}
