package com.eirora.spike.healthconnect.bridge

/**
 * Result of one bridge call. Implemented in C# with AndroidJavaProxy and called on a background thread.
 * The JSON can contain health data: it is only ever passed to the C# side in memory, never logged or stored.
 */
interface BridgeCallback {
    fun onSuccess(json: String)

    fun onError(code: String, message: String)
}
