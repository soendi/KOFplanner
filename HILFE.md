# KOFplanner – Hilfe

KOFplanner ist eine Desktop-Anwendung (Windows) zur Planung von Baustellen,
Teams, Mitarbeitern und Fahrzeugen. Sie verwaltet Einsatzpläne im Kalender,
berücksichtigt Urlaub und Krankheit und hilft beim Zuweisen von Fahrzeugen
anhand von Führerscheinklassen.

---

## 1. Übersicht der Oberfläche

Die Anwendung ist in vier Registerkarten (Tabs) unterteilt:

| Tab | Inhalt |
|-----|--------|
| **Kalender** | Monats- / Wochenansicht der Einsatzplanung, Tagesübersicht, Drag&Drop-Zuweisung |
| **Mitarbeiter & Teams** | Mitarbeiterliste, Teamkarten (per Drag&Drop befüllt), Fahrzeugliste |
| **Baustellen** | Verwaltung der Baustellen |
| **Mitarbeiter informieren** | E-Mail-Versand an ausgewählte Mitarbeiter / Teams / Baustellen |

Über das Menü **Datei** und **Hilfe** (oben) sind Backup, Wiederherstellung,
Update-Suche und Infos erreichbar.

---

## 2. Kalender

### 2.1 Navigation
- **Monats- / Wochenansicht**: Umschalten über den Button „Wochenansicht" /
  „Monatsansicht" oben im Kalender-Tab.
- **Vor / Zurück**: Pfeil-Buttons zum Blättern von Monat zu Monat bzw.
  Woche zu Woche.
- **Heute**: Springt zum aktuellen Monat.

### 2.2 Zuweisungen erstellen (Drag&Drop)
1. Klicken Sie in den Kalender und ziehen Sie über einen Bereich von Tagen
   (einzelner Tag oder mehrere Tage).
2. Es öffnet sich ein Aktionsfenster mit den Optionen:
   - **Baustelle zuweisen** – wählt Baustelle + Team (oder Baustelle + einzelner
     Mitarbeiter).
   - **Team zuweisen** – wählt Team + Baustelle.
   - **Mitarbeiter zuweisen** – wählt Mitarbeiter + Baustelle.
   - **Urlaub eintragen** / **Krankheit eintragen** – für einen Zeitraum.
3. Hat ein Team ein bevorzugtes Fahrzeug (`PreferredVehicleId`), wird dieses
   **automatisch und ohne Rückfrage** für den Zeitraum zugewiesen, sofern es an
   den Tagen noch frei ist. Andernfalls wird bei verfügbaren Fahrern gefragt,
   welches Fahrzeug zugewiesen wird.

### 2.3 Tagesübersicht
- **Doppelklick** auf eine Kalenderzelle öffnet die Übersicht für diesen Tag.
- Die Übersicht listet pro Baustelle:
  - **Team** (zeilenweise, mit rotem **X** zum Löschen)
  - **Fahrzeug** (zeilenweise, mit rotem **X**)
  - **Mitarbeiter** (einzeln zeilenweise, mit rotem **X**)
  - **Urlaub** und **Krankheit** des Tages (mit rotem **X** löschbar)
- Mehrfachtermine (über mehrere Tage) zeigen den Spannenhinweis
  `(dd.MM.–dd.MM.)`.

### 2.4 Löschen und Anpassen von Terminen
Alle löschbaren Einträge haben ein rotes **X** rechts in der Zeile.

- **Team löschen**: Es wird gefragt, ob der *ganze Termin* oder *nur dieser Tag*
  gelöscht wird. Beim Löschen eines Teams wird über denselben Zeitraum auch das
  **Fahrzeug** und die **Baustelle** mit entfernt (ein Fahrzeug ohne Team bzw.
  eine Baustelle ohne Fahrzeug+Team sind nicht sinnvoll).
- **Fahrzeug löschen**: Abfrage „Fahrzeug löschen?" →
  - **Ja** = Fahrzeug aus den Zeilen entfernen (Baustelle/Team bleiben).
  - **Nein** = anderes Fahrzeug zuweisen. Es werden nur Fahrzeuge angeboten,
    die an **keinem** der Tage des Zeitraums schon anderswo zugewiesen sind.
- **Mitarbeiter löschen**: Löscht die Person aus dem Termin. Ist die Person die
  **letzte mit dem erforderlichen Führerschein** für das verknüpfte Fahrzeug,
  erscheint ein Dialog mit den Optionen:
  - *Nicht löschen*
  - *Löschen* (Termin bleibt fahrerlos)
  - *Anderes Fahrzeug wählen* (Person löschen + freies Ersatzfahrzeug zuweisen)
  - *Ganzen Termin löschen*

Nach jeder Lösch- oder Anpassungsaktion wird der Kalender automatisch neu
geladen.

---

## 3. Mitarbeiter & Teams

### 3.1 Mitarbeiter
- Liste aller Mitarbeiter (Standard-Windows-Liste).
- **Neu**: legt einen neuen Mitarbeiter an.
- **Bearbeiten**: öffnet die Mitarbeiter-Maske (Doppelklick auf einen Eintrag
  öffnet sie ebenfalls). Hier werden Name, Führerscheinklassen
  (PKW, 3.5t, 7.5t, LKW, Anhänger) und Urlaub/Krankheit verwaltet.
- **Löschen**: entfernt den ausgewählten Mitarbeiter.
- **Drag&Drop**: einen Mitarbeiter auf eine Teamkarte ziehen, fügt ihn dem Team
  hinzu.

### 3.2 Teams
- Teams werden als Karten dargestellt.
- **Team erstellen**: über „Neues Team" (oder durch Ziehen eines Mitarbeiters
  auf die „Team erstellen"-Ablagezone).
- **Mitglieder**: per Drag&Drop aus der Mitarbeiterliste hinzufügen; innerhalb
  der Teamkarte entfernen.
- **Fahrzeug zuweisen**: weist dem Team ein bevorzugtes Fahrzeug zu. Es wird
  gewarnt, wenn kein Teammitglied die benötigte Führerscheinklasse besitzt.
- Die Teamfarbe wird im Kalender zur Kennzeichnung verwendet.

### 3.3 Fahrzeuge
- Liste aller Fahrzeuge mit benötigter Führerscheinklasse
  (`RequiredLicense`) und Kennzeichen.
- **Neu / Bearbeiten / Löschen** über die Buttons der Fahrzeugliste.

---

## 4. Baustellen

- Verwaltung der Baustellen (Name, Adresse, etc.).
- **Neu / Bearbeiten / Löschen** mit rotem **X** an jeder Zeile.
- Doppelklick auf eine Zeile öffnet die Bearbeitung.

---

## 5. Mitarbeiter informieren (E-Mail)

- Auswahl von Baustellen, Teams und einzelnen Mitarbeitern über
  direkte Checkboxen (gruppiert in einer GroupBox).
- Pro Mitarbeiter wird **eine E-Mail** erstellt (auch bei Mehrfachzuordnung
  zu mehreren Teams/Baustellen).
- Teammitglieder werden zu ihrem jeweiligen Mitarbeiter aufgelöst.

---

## 6. Urlaub und Krankheit

- Werden im Aktionsfenster des Kalenders (Drag&Drop-Bereich) oder in der
  Mitarbeiter-Maske eingetragen.
- Beim Eintragen wird geprüft, ob der Mitarbeiter im gewählten Zeitraum
  **bereits eingeteilt** ist:
  - **Keine Überschneidung** → Eintrag wird einfach gespeichert.
  - **Überschneidung** → Meldung mit den konflikttragenden Tagen und Auswahl:
    - *Nicht eintragen*
    - *Eintragen und Mitarbeiter aus dem/den Team(s) entfernen*
    - *Abbrechen*
- Urlaub/Krankheit sind in der Tagesübersicht einzeln löschbar.

---

## 7. Daten & Backup

Über das Menü **Datei**:

- **Datenbank sichern...**: erstellt ein lokales Backup der Datenbankdatei
  (`*.db`) neben der originalen Datei.
- **Datenbank wiederherstellen...**: ersetzt die aktuelle Datenbank durch eine
  ausgewählte Sicherung (vorher wird automatisch ein Backup der aktuellen
  Datei erstellt).
- **Google Drive Backup...**: konfiguriert und führt ein Backup in Google
  Drive aus (Ordner-ID, Tabellenblatt-ID und Dateiname werden abgefragt).

Über das Menü **Hilfe**:

- **Nach Updates suchen...**: prüft auf eine neue Version.
- **Info**: zeigt Version und Autor an.

---

## 8. Hinweise zur Datenhaltung

- Alle Daten werden in einer lokalen SQLite-Datenbank (`*.db`) gespeichert.
- Eine Zuweisung (Assignment) verknüpft optional Baustelle, Team, Fahrzeug
  und Mitarbeiter für ein bestimmtes Datum. Mehrfachtermine erstrecken sich
  über mehrere Einzelzuweisungen (eine pro Tag).
- Fahrzeuge ohne zugeordnetes Team und Baustelle ohne Fahrzeug+Team werden
  beim Löschen entsprechend mitentfernt, da sie allein nicht sinnvoll sind.

---

## 9. Tastatur & Maus

- **Doppelklick** auf Kalenderzelle → Tagesübersicht.
- **Ziehen** über Kalenderzellen → Zeitraum-Aktionsmenü.
- **Ziehen** eines Mitarbeiters auf eine Teamkarte → Teamzuordnung.
- **Doppelklick** auf Listen-/Karteneinträge → Bearbeiten.
- **Rotes X** in einer Zeile → Löschen dieses Eintrags.

---

*KOFplanner – Einsatzplanung für Baustellen, Teams, Mitarbeiter und Fahrzeuge.*
