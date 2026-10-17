using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Eirora.Spike.HealthConnect
{
    /// <summary>
    /// The spike's only screen (IMGUI, so that everything is code). Reads and shows Health Connect data on the device;
    /// it never logs or stores values, and the app has no internet permission.
    /// </summary>
    public sealed class SpikeScreen : MonoBehaviour
    {
        private const int PageSize = 40;
        private const int MaxPairsShown = 5;
        private static readonly int[] RangeDays = { 1, 7, 30, 90, 365 };
        private static readonly string[] Tabs = { "Status", "Steg", "Träning", "Sömn", "Dubbletter", "Ändringar", "Bakgrund", "Rapport" };
        private static readonly Habit[] Habits = { Habit.Steps, Habit.Exercise, Habit.Sleep };
        private static readonly ReadPermission[] HabitPermissions = { ReadPermission.Steps, ReadPermission.Exercise, ReadPermission.Sleep };
        private static readonly ReadPermission[] ExtraPermissions = { ReadPermission.Background, ReadPermission.History };

        private readonly Queue<Action> _deferred = new Queue<Action>();
        private readonly Dictionary<Habit, HabitReading> _readings = new Dictionary<Habit, HabitReading>();
        private readonly Dictionary<Habit, int> _pages = new Dictionary<Habit, int>();
        private readonly TimeZoneInfo _zone = TimeZoneInfo.Local;

        private IHealthConnectSource _source = null!;
        private SpikeStyles? _styles;
        private HealthStatus? _status;
        private PermissionResult? _lastPermissionResult;
        private ReadPermission[]? _explainingPermissions;
        private ChangeSet? _changes;
        private BackgroundRun[]? _backgroundRuns;
        private string? _busy;
        private string? _message;
        private bool _messageIsError;
        private int _tab;
        private int _rangeIndex = 1;
        private Habit _duplicateHabit = Habit.Steps;
        private bool _changeTracking;
        private bool _copied;
        private Vector2 _scroll;

        // Touch scrolling: IMGUI scroll views only follow the mouse wheel and scrollbars.
        private bool _dragging;
        private bool _swallowRelease;
        private Vector2 _dragDistance;

        private static float Density => Application.isMobilePlatform && Screen.dpi > 0 ? Screen.dpi / 160f : Mathf.Max(1f, Screen.height / 780f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfMissing()
        {
            if (FindAnyObjectByType<SpikeScreen>() == null)
            {
                new GameObject(nameof(SpikeScreen)).AddComponent<SpikeScreen>();
            }
        }

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _source = new AndroidHealthConnectSource();
#else
            _source = new FakeHealthConnectSource(DateTimeOffset.Now);
#endif
            Application.targetFrameRate = 60;

            // A tool, not a game: keep the status bar with the clock and notifications.
            Screen.fullScreen = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            TintSystemBars(SpikeStyles.Background);
#endif
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Start() => Run("Läser status", RefreshStatus);

        // Coming back from Health Connect's settings, permissions may have changed.
        private void OnApplicationFocus(bool focused)
        {
            if (focused && _status != null && _busy == null)
            {
                Run("Läser status", RefreshStatus);
            }
        }

        private void Update()
        {
            while (_deferred.Count > 0)
            {
                _deferred.Dequeue()();
            }

            TrackTouchScroll();
        }

        private void OnGUI()
        {
            _styles ??= new SpikeStyles(Density);
            var s = _styles;
            SwallowReleaseAfterDrag();

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), s.BackgroundTexture);
            var safe = Screen.safeArea;
            var gutter = s.Px(16);
            var area = new Rect(safe.x + gutter, Screen.height - safe.yMax + s.Px(12), safe.width - (2 * gutter), safe.height - s.Px(24));
            GUILayout.BeginArea(area);

            GUILayout.Label("Health Connect-spike", s.Title);
            GUILayout.Label($"Källa: {_source.Name}. Hälsodata visas bara här och lämnar aldrig telefonen.", s.Small);
            GUILayout.Space(s.Px(8));
            DrawTabs(s);
            DrawBanner(s);

            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUIStyle.none);
            GUILayout.Space(s.Px(4));
            switch (_tab)
            {
                case 0:
                    DrawStatus(s);
                    break;
                case 1:
                case 2:
                case 3:
                    DrawHabit(s, Habits[_tab - 1]);
                    break;
                case 4:
                    DrawDuplicates(s);
                    break;
                case 5:
                    DrawChanges(s);
                    break;
                case 6:
                    DrawBackground(s);
                    break;
                default:
                    DrawReport(s);
                    break;
            }

            GUILayout.Space(s.Px(24));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTabs(SpikeStyles s)
        {
            const int perRow = 4;
            for (var row = 0; row < Tabs.Length / perRow; row++)
            {
                var first = row * perRow;
                var clicked = ButtonRow(s, Tabs.Skip(first).Take(perRow).ToArray(), i => first + i == _tab ? s.SelectedTab : s.Tab);
                if (clicked >= 0)
                {
                    Defer(() =>
                    {
                        _tab = first + clicked;
                        _scroll = Vector2.zero;
                        _copied = false;
                        _message = null;
                    });
                }
            }

            GUILayout.Space(s.Px(4));
        }

        private void DrawBanner(SpikeStyles s)
        {
            if (_busy != null)
            {
                GUILayout.Label($"{_busy} …", s.WarningText);
            }
            else if (_message != null)
            {
                GUILayout.Label(_message, _messageIsError ? s.ErrorText : s.GoodText);
            }
        }

        private void DrawStatus(SpikeStyles s)
        {
            if (_explainingPermissions != null)
            {
                DrawExplanation(s, _explainingPermissions);
                return;
            }

            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label("Health Connect", s.Heading);
            if (_status == null)
            {
                GUILayout.Label("Inte läst ännu.", s.Body);
            }
            else
            {
                GUILayout.Label(SpikeText.SdkStatus(_status.sdkStatus), _status.sdkStatus == "SDK_AVAILABLE" ? s.GoodText : s.ErrorText);
                GUILayout.Label($"{_status.deviceModel}, Android {_status.androidRelease} (API {_status.androidSdk})", s.Body);
                foreach (var version in _status.providerVersions)
                {
                    GUILayout.Label(version, s.Small);
                }

                if (_status.sdkStatus == "SDK_AVAILABLE")
                {
                    GUILayout.Label($"Bakgrundsläsning: {Feature(_status.backgroundFeature)} · Historik: {Feature(_status.historyFeature)}", s.Small);
                }
            }

            GUILayout.EndVertical();

            if (_status?.sdkStatus == "SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED" && GUILayout.Button("Installera eller uppdatera Health Connect", s.Button))
            {
                Defer(() => Report(_source.OpenProviderInstall(), "Play Butik öppnades.", "Kunde inte öppna Play Butik."));
            }

            if (_status?.sdkStatus != "SDK_AVAILABLE")
            {
                if (GUILayout.Button("Läs status igen", s.SecondaryButton))
                {
                    Run("Läser status", RefreshStatus);
                }

                return;
            }

            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label("Behörigheter", s.Heading);
            foreach (var permission in HabitPermissions.Concat(ExtraPermissions))
            {
                var granted = IsGranted(permission);
                GUILayout.BeginHorizontal();
                GUILayout.Label(Capitalize(SpikeText.PermissionName(permission)), s.Body);
                GUILayout.FlexibleSpace();
                GUILayout.Label(granted ? "Beviljad" : "Inte beviljad", granted ? s.GoodText : s.BodyMuted, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }

            if (_lastPermissionResult != null)
            {
                GUILayout.Space(s.Px(6));
                GUILayout.Label(PermissionOutcome(_lastPermissionResult), s.Small);
            }

            GUILayout.EndVertical();

            if (GUILayout.Button("Ge behörighet för steg, träning och sömn", s.Button))
            {
                Defer(() => _explainingPermissions = HabitPermissions);
            }

            var extrasAvailable = _status.backgroundFeature == "AVAILABLE" || _status.historyFeature == "AVAILABLE";
            GUI.enabled = extrasAvailable;
            if (GUILayout.Button("Ge behörighet för bakgrund och historik", s.SecondaryButton))
            {
                Defer(() => _explainingPermissions = ExtraPermissions.Where(FeatureAvailable).ToArray());
            }

            GUI.enabled = true;
            if (!extrasAvailable)
            {
                GUILayout.Label("Den här versionen av Health Connect stöder varken bakgrundsläsning eller historik, så de kan inte begäras.", s.Small);
            }

            if (GUILayout.Button("Öppna Health Connect för den här appen", s.SecondaryButton))
            {
                Defer(() => Report(_source.OpenHealthConnectSettings(), "Health Connect öppnades.", "Kunde inte öppna Health Connect."));
            }

            if (GUILayout.Button("Läs status igen", s.SecondaryButton))
            {
                Run("Läser status", RefreshStatus);
            }
        }

        // HLT-03: say why before the system dialog asks.
        private void DrawExplanation(SpikeStyles s, ReadPermission[] permissions)
        {
            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label("Innan Health Connect frågar", s.Heading);
            if (permissions.Contains(ReadPermission.Background))
            {
                GUILayout.Label("Bakgrundsläsning låter spiken pröva om data kan läsas när appen är stängd, som spelet behöver för att krafterna ska vara redo vid gryningen.", s.Body);
                GUILayout.Label("Historik låter spiken läsa data som är äldre än 30 dagar före första behörigheten.", s.Body);
            }
            else
            {
                GUILayout.Label("Spiken läser steg, träningspass och sömn för att se vilka källor och inspelningsmetoder din telefon har.", s.Body);
                GUILayout.Label("Du kan välja bort vilken som helst av dem; det som nekas visas bara som saknat.", s.Body);
            }

            GUILayout.Space(s.Px(6));
            GUILayout.Label("Inget skrivs till Health Connect, inget sparas och appen saknar internetbehörighet.", s.Small);
            GUILayout.EndVertical();

            if (GUILayout.Button("Fortsätt till Health Connect", s.Button))
            {
                Defer(() => _explainingPermissions = null);
                Run("Väntar på Health Connect", () => RequestPermissions(permissions));
            }

            if (GUILayout.Button("Inte nu", s.SecondaryButton))
            {
                Defer(() => _explainingPermissions = null);
            }
        }

        private void DrawHabit(SpikeStyles s, Habit habit)
        {
            var permission = HealthKeys.PermissionFor(habit);
            if (_status?.sdkStatus != "SDK_AVAILABLE")
            {
                Card(s, "Health Connect saknas", "Se fliken Status.");
                return;
            }

            if (!IsGranted(permission))
            {
                Card(s, $"Ingen behörighet för {SpikeText.PermissionName(permission)}", "Spiken fungerar ändå; ge behörighet under Status för att läsa data.");
                if (GUILayout.Button("Till Status", s.SecondaryButton))
                {
                    Defer(() => _tab = 0);
                }

                return;
            }

            var range = ButtonRow(s, RangeDays.Select(d => $"{d} d").ToArray(), i => i == _rangeIndex ? s.SelectedTab : s.Tab);
            if (range >= 0)
            {
                Defer(() => _rangeIndex = range);
            }
            var days = RangeDays[_rangeIndex];
            if (GUILayout.Button($"Läs {SpikeText.HabitName(habit).ToLowerInvariant()} för {days} dagar", s.HabitButton(habit)))
            {
                Run($"Läser {SpikeText.HabitName(habit).ToLowerInvariant()}", () => Read(habit, days));
            }

            if (!_readings.TryGetValue(habit, out var reading))
            {
                GUILayout.Label("Inget läst ännu.", s.Small);
                return;
            }

            DrawSummary(s, reading);
            var records = reading.Page.records;
            if (records.Length == 0)
            {
                return;
            }

            _pages.TryGetValue(habit, out var page);
            var pages = (records.Length + PageSize - 1) / PageSize;
            page = Mathf.Clamp(page, 0, pages - 1);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Poster {(page * PageSize) + 1}–{Math.Min(records.Length, (page + 1) * PageSize)} av {records.Length}", s.Small);
            GUILayout.FlexibleSpace();
            GUI.enabled = page > 0;
            if (GUILayout.Button("Nyare", s.SecondaryButton, GUILayout.Width(s.Px(84))))
            {
                Defer(() => _pages[habit] = page - 1);
            }

            GUILayout.Space(s.Px(6));
            GUI.enabled = page < pages - 1;
            if (GUILayout.Button("Äldre", s.SecondaryButton, GUILayout.Width(s.Px(84))))
            {
                Defer(() => _pages[habit] = page + 1);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            foreach (var record in records.Skip(page * PageSize).Take(PageSize))
            {
                DrawRecord(s, record);
            }
        }

        private void DrawSummary(SpikeStyles s, HabitReading reading)
        {
            var records = reading.Page.records;
            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label($"{records.Length} poster på {reading.RangeDays} dagar{(reading.Page.truncated ? " (avkortat)" : "")}", s.Heading);
            if (records.Length > 0)
            {
                var oldest = DateTimeOffset.FromUnixTimeMilliseconds(records.Min(r => r.startMs));
                var age = (int)(DateTimeOffset.Now - oldest).TotalDays;
                GUILayout.Label($"Äldsta: {SpikeText.Timestamp(oldest.ToUnixTimeMilliseconds(), _zone)} ({(age == 0 ? "i dag" : age == 1 ? "i går" : $"{age} dagar sedan")})", s.Small);
            }

            foreach (var origin in records.GroupBy(r => r.origin).OrderByDescending(g => g.Count()))
            {
                GUILayout.Space(s.Px(6));
                var label = _source.AppLabel(origin.Key);
                GUILayout.Label($"{label}: {origin.Count()} poster", s.Body);
                if (label != origin.Key)
                {
                    GUILayout.Label(origin.Key, s.Small);
                }

                foreach (var method in origin.GroupBy(r => r.recordingMethod).OrderBy(g => g.Key))
                {
                    GUILayout.Label($"{RecordingMethods.Name(method.Key)} ({SpikeText.ClassName(RecordingMethods.Classify(method.Key))}): {method.Count()}", s.Small);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawRecord(SpikeStyles s, HealthRecord record)
        {
            var habit = record.Habit;
            GUILayout.BeginVertical(s.RaisedCardStyle);
            var heading = habit switch
            {
                Habit.Exercise => $"{record.exerciseTypeName} · {SpikeText.Value(record)}",
                _ => SpikeText.Value(record),
            };
            GUILayout.Label(heading, s.HabitHeading(habit));
            GUILayout.Label(SpikeText.Interval(record, _zone), s.Body);
            GUILayout.Label($"Källa: {_source.AppLabel(record.origin)}", s.Body);
            GUILayout.Label($"recordingMethod: {record.recordingMethodName} ({record.recordingMethod})", s.Body);
            GUILayout.Label($"Klassning: {SpikeText.ClassName(RecordingMethods.Classify(record.recordingMethod))}", s.Body);
            if (record.title.Length > 0)
            {
                GUILayout.Label($"Titel: {record.title}", s.Small);
            }

            if (habit == Habit.Exercise && (record.segmentCount > 0 || record.lapCount > 0))
            {
                GUILayout.Label($"{record.segmentCount} segment, {record.lapCount} varv", s.Small);
            }

            if (habit == Habit.Sleep)
            {
                GUILayout.Label(record.stages.Length == 0 ? "Inga sömnfaser" : $"Faser: {Stages(record)}", s.Small);
            }

            var device = record.hasDevice ? $"{record.deviceTypeName} {record.deviceManufacturer} {record.deviceModel}".Trim() : "ingen";
            GUILayout.Label($"Enhet: {device} · ändrad {SpikeText.Timestamp(record.lastModifiedMs, _zone)}", s.Small);
            GUILayout.Label($"{record.origin} · id {Shorten(record.id)}", s.Small);
            if (record.clientRecordId.Length > 0)
            {
                GUILayout.Label($"Klient-id {Shorten(record.clientRecordId)}, version {record.clientRecordVersion}", s.Small);
            }
            GUILayout.EndVertical();
        }

        private void DrawDuplicates(SpikeStyles s)
        {
            GUILayout.Label("Jämför den naiva summan av alla poster med Health Connects aggregat, som slår ihop överlappande källor efter prioritet. Poster räknas på dagen de börjar.", s.Small);
            GUILayout.Space(s.Px(4));
            var selected = ButtonRow(s, new[] { Tabs[1], Tabs[2], Tabs[3] }, i => Habits[i] == _duplicateHabit ? s.HabitButton(Habits[i]) : s.Tab);
            if (selected >= 0)
            {
                Defer(() => _duplicateHabit = Habits[selected]);
            }

            if (!_readings.TryGetValue(_duplicateHabit, out var reading))
            {
                Card(s, "Inget läst ännu", $"Läs {SpikeText.HabitName(_duplicateHabit).ToLowerInvariant()} på dess flik först.");
                return;
            }

            if (reading.Aggregates == null)
            {
                Card(s, "Aggregat saknas", "Health Connect gav inget aggregat för perioden.");
            }
            else
            {
                var readWindowStart = DuplicateAnalysis.LocalDay(DateTimeOffset.Now.AddDays(-30), _zone);
                foreach (var day in DuplicateAnalysis.CompareByDay(reading.Page.records, reading.Aggregates, _zone))
                {
                    DrawDay(s, day, string.CompareOrdinal(day.Day, readWindowStart) < 0);
                }
            }

            var overlaps = DuplicateAnalysis.FindOverlaps(reading.Page.records);
            var crossSource = overlaps.Where(o => !o.SameOrigin).ToList();
            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label("Överlappande poster", s.Heading);
            GUILayout.Label($"{crossSource.Count} par mellan olika källor, {overlaps.Count - crossSource.Count} inom samma källa", s.Body);
            foreach (var pair in crossSource.Take(MaxPairsShown))
            {
                GUILayout.Space(s.Px(6));
                GUILayout.Label($"{_source.AppLabel(pair.First.origin)}: {SpikeText.Interval(pair.First, _zone)}, {SpikeText.Value(pair.First)}", s.Small);
                GUILayout.Label($"{_source.AppLabel(pair.Second.origin)}: {SpikeText.Interval(pair.Second, _zone)}, {SpikeText.Value(pair.Second)}", s.Small);
                GUILayout.Label($"Överlapp {SpikeText.Duration(pair.Duration)}", s.Small);
            }

            if (crossSource.Count > MaxPairsShown)
            {
                GUILayout.Space(s.Px(6));
                GUILayout.Label($"… och {crossSource.Count - MaxPairsShown} par till", s.Small);
            }

            GUILayout.EndVertical();
        }

        private void DrawDay(SpikeStyles s, DayComparison day, bool beforeReadWindow)
        {
            GUILayout.BeginVertical(s.RaisedCardStyle);
            GUILayout.Label(SpikeText.Day(day.Day), s.HabitHeading(_duplicateHabit));
            var aggregate = day.Aggregate is { hasValue: true } found ? SpikeText.Value(_duplicateHabit, found.value) : "saknas";
            GUILayout.Label($"Rå summa {SpikeText.Value(_duplicateHabit, day.RawTotal)} · Health Connect {aggregate}", s.Body);
            if (day.Surplus is long surplus && surplus != 0)
            {
                GUILayout.Label($"Skillnad: {SpikeText.Value(_duplicateHabit, surplus)}", surplus > 0 ? s.WarningText : s.Small);
            }

            if (beforeReadWindow && day.RawTotal == 0)
            {
                GUILayout.Label("Mer än 30 dagar sedan: aggregatet når längre bak än posterna.", s.Small);
            }

            foreach (var origin in day.RawByOrigin.OrderByDescending(o => o.Value))
            {
                GUILayout.Label($"{_source.AppLabel(origin.Key)}: {SpikeText.Value(_duplicateHabit, origin.Value)}", s.Small);
            }

            if (day.Aggregate != null && day.Aggregate.origins.Length > 0)
            {
                GUILayout.Label($"Aggregatet bygger på: {string.Join(", ", day.Aggregate.origins.Select(_source.AppLabel))}", s.Small);
            }

            GUILayout.EndVertical();
        }

        private void DrawChanges(SpikeStyles s)
        {
            Card(s, "Sen eller ändrad källdata (HLT-04)", "Starta spårning, ändra eller radera sedan en post i källappen (till exempel ett träningspass) och hämta ändringarna här.");
            var habits = Habits.Where(h => IsGranted(HealthKeys.PermissionFor(h))).ToArray();
            if (habits.Length == 0)
            {
                GUILayout.Label("Ge behörighet för minst en vana under Status först.", s.Small);
                return;
            }

            if (GUILayout.Button(_changeTracking ? "Starta om spårningen" : $"Starta spårning av {string.Join(", ", habits.Select(h => SpikeText.HabitName(h).ToLowerInvariant()))}", s.Button))
            {
                Run("Startar spårning", async () =>
                {
                    await _source.StartChangeTrackingAsync(habits);
                    _changeTracking = true;
                    _changes = null;
                    Succeed("Spårningen är startad. Ändra något i källappen och hämta sedan.");
                });
            }

            if (GUILayout.Button("Hämta ändringar", s.SecondaryButton))
            {
                Run("Hämtar ändringar", async () =>
                {
                    _changes = await _source.ReadChangesAsync();
                    _changeTracking = !_changes.expired;
                    Succeed(_changes.expired ? "Ändringstoken hade gått ut; starta spårningen igen." : $"{_changes.upserts.Length} nya eller ändrade, {_changes.deletions.Length} raderade.");
                });
            }

            if (_changes == null)
            {
                return;
            }

            foreach (var record in _changes.upserts.Take(PageSize))
            {
                DrawRecord(s, record);
            }

            if (_changes.deletions.Length > 0)
            {
                GUILayout.BeginVertical(s.CardStyle);
                GUILayout.Label($"Raderade ({_changes.deletions.Length})", s.Heading);
                GUILayout.Label("En radering har bara postens id, ingen vana eller tid.", s.Small);
                foreach (var id in _changes.deletions.Take(PageSize))
                {
                    GUILayout.Label($"id {id}", s.Small);
                }

                GUILayout.EndVertical();
            }
        }

        private void DrawBackground(SpikeStyles s)
        {
            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label("Bakgrundsläsning", s.Heading);
            GUILayout.Label("En schemalagd körning läser senaste dygnets steg via WorkManager och sparar bara tid, utfall och antal poster.", s.Small);
            GUILayout.Label($"Funktionen: {Feature(_status?.backgroundFeature ?? "")} · Behörighet: {(IsGranted(ReadPermission.Background) ? "beviljad" : "inte beviljad")}", s.Body);
            if (!IsGranted(ReadPermission.Background))
            {
                GUILayout.Label("Utan behörighet ser en körning i bakgrunden bara appens egen data, alltså 0 poster, utan något fel.", s.Small);
            }

            GUILayout.EndVertical();

            if (GUILayout.Button("Kör en gång om 60 sekunder", s.Button))
            {
                Defer(() =>
                {
                    _source.RunBackgroundReadOnce(TimeSpan.FromSeconds(60));
                    Succeed("Schemalagd. Lämna appen nu och kom tillbaka om ett par minuter.");
                });
            }

            if (GUILayout.Button("Kör var 15:e minut", s.SecondaryButton))
            {
                Defer(() =>
                {
                    _source.ScheduleBackgroundRead();
                    Succeed("Schemalagt var 15:e minut tills du stoppar.");
                });
            }

            if (GUILayout.Button("Stoppa schemalagda körningar", s.SecondaryButton))
            {
                Defer(() =>
                {
                    _source.CancelBackgroundRead();
                    Succeed("Stoppat.");
                });
            }

            GUILayout.Space(s.Px(4));
            switch (ButtonRow(s, new[] { "Uppdatera logg", "Rensa logg" }, _ => s.SecondaryButton))
            {
                case 0:
                    Run("Läser loggen", RefreshBackgroundRuns);
                    break;
                case 1:
                    Defer(() =>
                    {
                        _source.ClearBackgroundRuns();
                        _backgroundRuns = Array.Empty<BackgroundRun>();
                    });
                    break;
            }

            if (_backgroundRuns == null)
            {
                return;
            }

            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label($"Körningar ({_backgroundRuns.Length})", s.Heading);
            if (_backgroundRuns.Length == 0)
            {
                GUILayout.Label("Inga ännu.", s.Small);
            }

            foreach (var run in _backgroundRuns.OrderByDescending(r => r.atMs))
            {
                var where = run.InBackground ? "i bakgrunden" : "med appen öppen";
                var outcome = run.outcome == "ok" ? $"ok, {run.recordCount} poster" : run.outcome;
                var style = run.outcome != "ok" ? s.ErrorText : run.recordCount == 0 ? s.WarningText : s.Body;
                GUILayout.Label($"{SpikeText.Timestamp(run.atMs, _zone)} · {where} · {outcome}", style);
            }

            GUILayout.EndVertical();
        }

        private void DrawReport(SpikeStyles s)
        {
            var report = SpikeReport.Build(_status, _readings, _backgroundRuns, _changes, DateTimeOffset.Now, _zone);
            Card(s, "Rapport till ärendet", "Innehåller källor, inspelningsmetoder, antal och beteenden men inga hälsovärden, tider eller titlar. Läs igenom innan du delar.");
            if (GUILayout.Button(_copied ? "Kopierad" : "Kopiera rapporten", s.Button))
            {
                Defer(() =>
                {
                    GUIUtility.systemCopyBuffer = report;
                    _copied = true;
                });
            }

            GUILayout.BeginVertical(s.RaisedCardStyle);
            GUILayout.Label(report, s.Small);
            GUILayout.EndVertical();
        }

        private async Task RefreshStatus()
        {
            _status = await _source.GetStatusAsync();
        }

        private async Task RequestPermissions(ReadPermission[] permissions)
        {
            var result = await _source.RequestPermissionsAsync(permissions);
            _status = await _source.GetStatusAsync();
            _lastPermissionResult = result;
        }

        private async Task Read(Habit habit, int days)
        {
            var to = DateTimeOffset.Now;
            var from = to.AddDays(-days);
            var page = await _source.ReadRecordsAsync(habit, from, to);
            DayAggregate[]? aggregates = null;
            string? aggregateError = null;
            try
            {
                aggregates = await _source.AggregateByDayAsync(habit, from, to);
            }
            catch (HealthConnectException e)
            {
                aggregateError = e.Message;
            }

            _readings[habit] = new HabitReading(habit, days, page, aggregates);
            _pages[habit] = 0;
            _duplicateHabit = habit;
            if (aggregateError != null)
            {
                Fail($"{page.records.Length} poster lästa, men aggregatet misslyckades: {aggregateError}");
            }
            else
            {
                Succeed($"{page.records.Length} poster lästa.");
            }
        }

        private async Task RefreshBackgroundRuns()
        {
            _backgroundRuns = await _source.GetBackgroundRunsAsync();
            Succeed($"{_backgroundRuns.Length} körningar i loggen.");
        }

        /// <summary>Runs one action at a time outside OnGUI, so the layout never changes between IMGUI's passes.</summary>
        private void Run(string label, Func<Task> action)
        {
            Defer(async () =>
            {
                if (_busy != null)
                {
                    return;
                }

                _busy = label;
                _message = null;
                try
                {
                    await action();
                }
                catch (HealthConnectException e)
                {
                    Fail(e.PermissionDenied ? $"Behörighet saknas: {e.Message}" : $"{e.Code}: {e.Message}");
                }
                catch (Exception e)
                {
                    // Bridge and parsing failures carry no health values, only types and permission names.
                    Fail($"Oväntat fel: {e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    _busy = null;
                }
            });
        }

        private void Defer(Action action) => _deferred.Enqueue(action);

        /// <summary>Equal-width buttons in one row, flush with the cards' edges. Returns the index pressed, or -1.</summary>
        private static int ButtonRow(SpikeStyles s, IReadOnlyList<string> labels, Func<int, GUIStyle> style)
        {
            var gap = s.Px(6);
            var height = s.Px(44);
            var row = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            var width = (row.width - (gap * (labels.Count - 1))) / labels.Count;
            var pressed = -1;
            for (var i = 0; i < labels.Count; i++)
            {
                if (GUI.Button(new Rect(row.x + (i * (width + gap)), row.y, width, height), labels[i], style(i)))
                {
                    pressed = i;
                }
            }

            GUILayout.Space(gap);
            return pressed;
        }

        private void Succeed(string message)
        {
            _message = message;
            _messageIsError = false;
        }

        private void Fail(string message)
        {
            _message = message;
            _messageIsError = true;
        }

        private void Report(bool opened, string success, string failure)
        {
            if (opened)
            {
                Succeed(success);
            }
            else
            {
                Fail(failure);
            }
        }

        private bool FeatureAvailable(ReadPermission permission) => permission switch
        {
            ReadPermission.Background => _status?.backgroundFeature == "AVAILABLE",
            ReadPermission.History => _status?.historyFeature == "AVAILABLE",
            _ => true,
        };

        private bool IsGranted(ReadPermission permission) => _status != null && _status.granted.Contains(HealthKeys.AndroidPermission(permission));

        // Judged against the current status, so it stays true after the player changes permissions in Health Connect.
        private string PermissionOutcome(PermissionResult result)
        {
            var requested = ((ReadPermission[])Enum.GetValues(typeof(ReadPermission))).Where(p => result.requested.Contains(HealthKeys.AndroidPermission(p))).ToList();
            var denied = requested.Where(p => !IsGranted(p)).ToList();
            if (denied.Count == 0)
            {
                return $"Alla {requested.Count} begärda behörigheter är beviljade.";
            }

            return $"{requested.Count - denied.Count} av {requested.Count} beviljade; nekade: {string.Join(", ", denied.Select(SpikeText.PermissionName))}. Efter två nekanden visar Android inte dialogen igen; ändra då i Health Connect.";
        }

        private void Card(SpikeStyles s, string heading, string body)
        {
            GUILayout.BeginVertical(s.CardStyle);
            GUILayout.Label(heading, s.Heading);
            GUILayout.Label(body, s.Small);
            GUILayout.EndVertical();
        }

        private void TrackTouchScroll()
        {
            if (Input.touchCount != 1 || _styles == null)
            {
                _dragging = false;
                return;
            }

            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _dragDistance = Vector2.zero;
                    _swallowRelease = false;
                    break;
                case TouchPhase.Moved:
                    _dragDistance += touch.deltaPosition;
                    if (_dragging || _dragDistance.magnitude > _styles.Px(10))
                    {
                        _dragging = true;
                        _scroll.y = Mathf.Max(0f, _scroll.y + touch.deltaPosition.y);
                    }

                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    _swallowRelease = _dragging;
                    _dragging = false;
                    break;
            }
        }

        // A drag that ends on a button must not press it.
        private void SwallowReleaseAfterDrag()
        {
            var current = Event.current;
            if ((_dragging || _swallowRelease) && (current.type == EventType.MouseDrag || current.type == EventType.MouseUp))
            {
                if (current.type == EventType.MouseUp)
                {
                    _swallowRelease = false;
                }

                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void TintSystemBars(Color color)
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bars = new AndroidJavaClass("com.eirora.spike.healthconnect.bridge.SystemBars");
            Color32 c = color;
            bars.CallStatic("tint", activity, (c.a << 24) | (c.r << 16) | (c.g << 8) | c.b);
        }

#endif
        private static string Stages(HealthRecord record) => string.Join(", ", record.stages
            .GroupBy(stage => stage.stageName)
            .Select(g => $"{g.Key} {SpikeText.Duration(TimeSpan.FromMilliseconds(g.Sum(stage => stage.endMs - stage.startMs)))}"));

        private static string Feature(string status) => status switch
        {
            "AVAILABLE" => "finns",
            "UNAVAILABLE" => "saknas",
            _ => "okänd",
        };

        private static string Shorten(string id) => id.Length > 12 ? id.Substring(0, 8) + "…" : id;

        private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
