package com.eirora.spike.healthconnect.bridge

import android.app.Activity
import android.os.Bundle
import android.util.TypedValue
import android.widget.ScrollView
import android.widget.TextView

/** The explanation Health Connect links to from its permission dialog and settings ("Läs integritetspolicyn"). */
class HealthPrivacyActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        title = "Eirora HC-spike och hälsodata"
        val padding = TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, 24f, resources.displayMetrics).toInt()
        val text = TextView(this).apply {
            setPadding(padding, padding, padding, padding)
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 16f)
            setTextColor(0xFF1C1B1F.toInt())
            setLineSpacing(0f, 1.25f)
            text = PRIVACY_TEXT
        }
        // Explicit colours: the activity has no theme of its own and must stay readable in dark mode.
        setContentView(ScrollView(this).apply {
            setBackgroundColor(0xFFFFFFFF.toInt())
            addView(text)
        })
    }

    private companion object {
        const val PRIVACY_TEXT = """Eirora HC-spike är en testapp för att undersöka Health Connect inför spelet Eirora.

Appen läser steg, träningspass och sömn, och bara om du ger behörighet. Den skriver aldrig något till Health Connect.

Allt som läses visas bara på skärmen i den här telefonen. Appen har ingen behörighet att använda internet, så ingen hälsodata kan skickas någonstans. Inga hälsovärden sparas eller loggas.

Det enda appen sparar är en ändringsmarkör från Health Connect och resultatet av bakgrundsläsningar (tidpunkt, utfall och antal poster), aldrig själva värdena.

Du kan när som helst återkalla behörigheten i Health Connect. Avinstallerar du appen försvinner allt den sparat."""
    }
}
