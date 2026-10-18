package com.eirora.health

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.health.connect.client.PermissionController

/**
 * Invisible activity that shows the Health Connect permission dialog.
 * The permission contract needs a ComponentActivity, which Unity's own activity is not guaranteed to be.
 */
class HealthPermissionActivity : ComponentActivity() {
    private val launcher = registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
        complete(granted, null)
    }

    private val requestId get() = intent.getIntExtra(HealthBridge.EXTRA_REQUEST_ID, -1)
    private val requested get() = intent.getStringArrayExtra(HealthBridge.EXTRA_PERMISSIONS)?.toSet() ?: emptySet()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // After a configuration change the pending result is delivered to the recreated activity; do not ask twice.
        if (savedInstanceState == null) {
            try {
                launcher.launch(requested)
            } catch (e: Exception) {
                complete(null, e)
            }
        }
    }

    private fun complete(granted: Set<String>?, error: Exception?) {
        HealthBridge.completePermissionRequest(applicationContext, requestId, error)
        finish()
    }
}
