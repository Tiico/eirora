package com.eirora.signin

/**
 * Result of one bridge call. Implemented in C# with AndroidJavaProxy; exactly one method is called, once.
 * The value can be an ID token: it is only ever passed to the C# side in memory, never logged or stored here.
 */
interface SignInCallback {
    fun onSuccess(value: String)

    /** The player closed the dialog or the page. */
    fun onCancel()

    /** The reason only, never a token or an identifier. */
    fun onError(reason: String)
}
