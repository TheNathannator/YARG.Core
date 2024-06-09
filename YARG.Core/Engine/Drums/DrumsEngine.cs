﻿using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Drums
{
    public abstract class DrumsEngine : BaseEngine<DrumNote, DrumsEngineParameters,
        DrumsStats, DrumsEngineState>
    {
        public delegate void OverhitEvent();

        public delegate void PadHitEvent(DrumsAction action, bool noteWasHit);

        public OverhitEvent? OnOverhit;
        public PadHitEvent?  OnPadHit;

        protected DrumsEngine(InstrumentDifficulty<DrumNote> chart, SyncTrack syncTrack,
            DrumsEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, true, isBot)
        {
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            base.Reset(keepCurrentButtons);
        }

        public virtual void Overhit()
        {
            // Can't overhit before first note is hit/missed
            if (State.NoteIndex == 0)
            {
                return;
            }

            // Cancel overhit if past last note
            if (State.NoteIndex >= Chart.Notes.Count - 1)
            {
                return;
            }

            if (State.NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[State.NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[State.NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke();
        }

        protected override void HitNote(DrumNote note)
        {
            HitNote(note, false);
        }

        protected void HitNote(DrumNote note, bool activationAutoHit)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Pad: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Pad, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetHitState(true, false);

            // Detect if the last note(s) were skipped
            bool skipped = SkipPreviousNotes(note.ParentOrSelf);

            // Make sure that the note is fully hit, so the last hit note awards the starpower.
            if (note.IsStarPower && note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (State.IsSoloActive)
            {
                Solos[State.CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (!activationAutoHit && note.IsStarPowerActivator && CanStarPowerActivate && note.ParentOrSelf.WasFullyHit())
            {
                ActivateStarPower();
            }

            EngineStats.Combo++;

            if (EngineStats.Combo > EngineStats.MaxCombo)
            {
                EngineStats.MaxCombo = EngineStats.Combo;
            }

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            // If it's an auto hit, act as if it *wasn't* hit visually.
            // Score and such is accounted for above.
            if (!activationAutoHit)
            {
                OnNoteHit?.Invoke(State.NoteIndex, note);
            }

            base.HitNote(note);
        }

        protected override void MissNote(DrumNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Pad: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Pad, State.NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetMissState(true, false);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }
            if (note.IsSoloStart)
            {
                StartSolo();
            }

            EngineStats.Combo = 0;

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);
            base.MissNote(note);
        }

        protected int GetPointsPerNote()
        {
            return EngineParameters.Mode == DrumsEngineParameters.DrumMode.ProFourLane
                ? POINTS_PER_PRO_NOTE
                : POINTS_PER_NOTE;
        }

        protected override void AddScore(DrumNote note)
        {
            int pointsPerNote = GetPointsPerNote();
            AddScore(pointsPerNote * EngineStats.ScoreMultiplier);
        }

        protected sealed override int CalculateBaseScore()
        {
            int pointsPerNote = GetPointsPerNote();

            int score = 0;
            foreach (var note in Notes)
            {
                score += pointsPerNote * (1 + note.ChildNotes.Count);
            }

            return score;
        }

        protected static bool IsTomInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() switch
            {
                DrumsAction.RedDrum or
                    DrumsAction.YellowDrum or
                    DrumsAction.BlueDrum or
                    DrumsAction.GreenDrum => true,
                _ => false,
            };
        }

        protected static bool IsCymbalInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() switch
            {
                DrumsAction.YellowCymbal or
                    DrumsAction.BlueCymbal or
                    DrumsAction.OrangeCymbal or
                    DrumsAction.GreenCymbal => true,
                _ => false,
            };
        }

        protected static bool IsKickInput(GameInput input)
        {
            return input.GetAction<DrumsAction>() == DrumsAction.Kick;
        }

        protected static bool IsNoteInput(GameInput input)
        {
            return IsTomInput(input) || IsCymbalInput(input) || IsKickInput(input);
        }

        protected static int ConvertInputToPad(DrumsEngineParameters.DrumMode mode, DrumsAction action)
        {
            return mode switch
            {
                DrumsEngineParameters.DrumMode.NonProFourLane => action switch
                {
                    DrumsAction.Kick => (int) FourLaneDrumPad.Kick,

                    DrumsAction.RedDrum    => (int) FourLaneDrumPad.RedDrum,
                    DrumsAction.YellowDrum => (int) FourLaneDrumPad.YellowDrum,
                    DrumsAction.BlueDrum   => (int) FourLaneDrumPad.BlueDrum,
                    DrumsAction.GreenDrum  => (int) FourLaneDrumPad.GreenDrum,

                    DrumsAction.YellowCymbal => (int) FourLaneDrumPad.YellowDrum,
                    DrumsAction.BlueCymbal   => (int) FourLaneDrumPad.BlueDrum,
                    DrumsAction.GreenCymbal  => (int) FourLaneDrumPad.GreenDrum,

                    _ => -1
                },
                DrumsEngineParameters.DrumMode.ProFourLane => action switch
                {
                    DrumsAction.Kick => (int) FourLaneDrumPad.Kick,

                    DrumsAction.RedDrum    => (int) FourLaneDrumPad.RedDrum,
                    DrumsAction.YellowDrum => (int) FourLaneDrumPad.YellowDrum,
                    DrumsAction.BlueDrum   => (int) FourLaneDrumPad.BlueDrum,
                    DrumsAction.GreenDrum  => (int) FourLaneDrumPad.GreenDrum,

                    DrumsAction.YellowCymbal => (int) FourLaneDrumPad.YellowCymbal,
                    DrumsAction.BlueCymbal   => (int) FourLaneDrumPad.BlueCymbal,
                    DrumsAction.GreenCymbal  => (int) FourLaneDrumPad.GreenCymbal,

                    _ => -1
                },
                DrumsEngineParameters.DrumMode.FiveLane => action switch
                {
                    DrumsAction.Kick => (int) FiveLaneDrumPad.Kick,

                    DrumsAction.RedDrum   => (int) FiveLaneDrumPad.Red,
                    DrumsAction.BlueDrum  => (int) FiveLaneDrumPad.Blue,
                    DrumsAction.GreenDrum => (int) FiveLaneDrumPad.Green,

                    DrumsAction.YellowCymbal => (int) FiveLaneDrumPad.Yellow,
                    DrumsAction.OrangeCymbal => (int) FiveLaneDrumPad.Orange,

                    _ => -1
                },
                _ => throw new Exception("Unreachable.")
            };
        }
    }
}