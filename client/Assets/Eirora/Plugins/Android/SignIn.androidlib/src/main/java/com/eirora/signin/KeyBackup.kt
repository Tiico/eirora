package com.eirora.signin

import android.content.Context
import com.google.android.gms.auth.blockstore.Blockstore
import com.google.android.gms.auth.blockstore.DeleteBytesRequest
import com.google.android.gms.auth.blockstore.RetrieveBytesRequest
import com.google.android.gms.auth.blockstore.StoreBytesData
import com.google.android.gms.common.api.ApiException
import com.google.android.gms.tasks.Task
import com.google.android.gms.tasks.Tasks

/**
 * The contribution key in Block Store (ADR-0008, TASK-023b), so that a new Android phone restored from this one's
 * backup, or set up from it by cable or wirelessly, reads the contribution part without asking. The key goes to the
 * cloud backup only when Block Store can encrypt it end to end, which needs a screen lock; otherwise it stays on this
 * phone and goes along only in a transfer from phone to phone. Each call answers once through its [SignInCallback];
 * nothing here logs the key.
 */
object KeyBackup {
    /** What [keep] answers with: the key reaches the cloud backup end-to-end encrypted. */
    const val END_TO_END = "end-to-end"

    /** What [keep] answers with: the key stays on this phone. */
    const val THIS_PHONE = "this-phone"

    /** What [keep] answers with: the key stays on this phone, since Block Store could not say whether it encrypts end to end. */
    const val UNKNOWN = "unknown"

    /** Answers with the value kept under [name], or an empty string when there is none. */
    @JvmStatic
    fun read(context: Context, name: String, callback: SignInCallback) {
        val request = RetrieveBytesRequest.Builder().setKeys(listOf(name)).build()
        answer(Blockstore.getClient(context.applicationContext).retrieveBytes(request), callback) { response ->
            response.blockstoreDataMap[name]?.bytes?.toString(Charsets.UTF_8) ?: ""
        }
    }

    /**
     * Keeps [value] under [name] and answers with [END_TO_END], [THIS_PHONE] or [UNKNOWN]. When Block Store cannot say
     * whether it encrypts end to end, for example with outdated Play services, the key stays on this phone and the
     * answer is [UNKNOWN], so that the account sheet does not claim the phone has no screen lock.
     */
    @JvmStatic
    fun keep(context: Context, name: String, value: String, callback: SignInCallback) {
        val client = Blockstore.getClient(context.applicationContext)
        val kept = client.isEndToEndEncryptionAvailable.continueWithTask { available ->
            val reach = when {
                !available.isSuccessful -> UNKNOWN
                available.result == true -> END_TO_END
                else -> THIS_PHONE
            }
            val data = StoreBytesData.Builder()
                .setKey(name)
                .setBytes(value.toByteArray(Charsets.UTF_8))
                .setShouldBackupToCloud(reach == END_TO_END)
                .build()
            client.storeBytes(data).onSuccessTask { Tasks.forResult(reach) }
        }
        answer(kept, callback) { it }
    }

    /**
     * Removes what is kept under [name] on this phone, and from the cloud backup at the next device backup, and answers
     * with an empty string. A phone already set up from this one keeps the copy it was given (ADR-0008).
     */
    @JvmStatic
    fun forget(context: Context, name: String, callback: SignInCallback) {
        val request = DeleteBytesRequest.Builder().setKeys(listOf(name)).build()
        answer(Blockstore.getClient(context.applicationContext).deleteBytes(request), callback) { "" }
    }

    private fun <T> answer(task: Task<T>, callback: SignInCallback, value: (T) -> String) {
        task.addOnSuccessListener { result -> callback.onSuccess(value(result)) }
            .addOnFailureListener { error ->
                // The status code only; Block Store's messages are not ours to pass on.
                callback.onError(if (error is ApiException) "Block Store error ${error.statusCode}" else "Block Store failed")
            }
    }
}
