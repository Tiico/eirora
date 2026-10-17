package com.eirora.spike.healthconnect.bridge

import java.lang.reflect.Modifier

/** Turns Health Connect int constants (recording method, exercise type, sleep stage, device type) into their names. */
internal object ConstantNames {
    private val cache = HashMap<Pair<Class<*>, String>, Map<Int, String>>()

    @Synchronized
    fun of(owner: Class<*>, prefix: String, value: Int): String {
        val names = cache.getOrPut(owner to prefix) {
            owner.fields
                .filter { Modifier.isStatic(it.modifiers) && it.type == Int::class.javaPrimitiveType && it.name.startsWith(prefix) }
                .associate { it.getInt(null) to it.name.removePrefix(prefix) }
        }
        return names[value] ?: value.toString()
    }
}
