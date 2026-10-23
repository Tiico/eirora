package com.eirora.health

import android.app.Activity
import android.os.Bundle
import android.util.TypedValue
import android.widget.ScrollView
import android.widget.TextView

/** The explanation Health Connect links to from its permission dialog and settings (ux-journeys.md, step 9). */
class HealthPrivacyActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        title = "Eirora och hälsodata"
        val padding = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, 24f, resources.displayMetrics).toInt()
        val text = TextView(this).apply {
            setPadding(padding, padding, padding, padding)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 16f)
            setTextColor(0xFF2B2A33.toInt())
            setLineSpacing(0f, 1.25f)
            text = PRIVACY_TEXT
        }
        // Explicit colours: the activity has no theme of its own and must stay readable in dark mode.
        setContentView(ScrollView(this).apply {
            setBackgroundColor(0xFFFBF7EF.toInt())
            addView(text)
        })
    }

    private companion object {
        const val PRIVACY_TEXT = """Eirora läser tre saker från Health Connect: hur många steg du tar, vilka träningspass du gör och hur länge du sover. Inget annat: ingen plats, ingen puls och ingen sömnkvalitet.

Det läses när du öppnar spelet, aldrig i bakgrunden, och Eirora skriver aldrig något till Health Connect.

Allt stannar på din telefon. Spelet sparar bara summor per dag, aldrig de enskilda posterna.

Du kan när som helst ta bort behörigheten i Health Connect. Spelet fungerar lika bra om du skriver in själv."""
    }
}
