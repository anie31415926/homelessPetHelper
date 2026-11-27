using System;

namespace HomelessPetHelper.Modules.Station.UI.Affection
{
    /// <summary>
    /// Facial reaction the pet shows for the current affection input.
    /// </summary>
    public enum AffectionFace
    {
        Calm,
        Smile,
        Laugh
    }

    /// <summary>
    /// Pure, deterministic affection reaction state machine.
    /// Every method takes an explicit monotonic timestamp in seconds, so the whole reaction
    /// can be replayed or unit tested without a game loop. Holds plain data only, no Unity types.
    /// Distances are art units where 512 units is a full drag.
    /// </summary>
    public sealed class PetAffectionReaction
    {

        private const float MaxStepDistance = 36f;   // per event cap, art units
        private const float SmileDistance = 38f;    // streak distance needed to smile
        private const float SmileTime = 0.16f;      // streak age needed to smile
        private const float LaughDistance = 100f;   // streak distance needed to laugh
        private const float LaughTime = 0.54f;      // streak age needed to laugh
        private const float SmileVisibleTime = 0.26f; // smile must be on screen this long before laughing
        private const float StreakGap = 0.42f;      // a longer pause drops the partial streak
        private const float LaughLife = 0.78f;      // laugh lives this long past the last move
        private const float PulseGapTime = 0.65f;   // repeat pulse: minimum time since last pulse
        private const float PulseGapDistance = 56f; // repeat pulse: minimum distance since last pulse
        private const float SmileLinger = 1.8f;     // smile survives this long on lift / idle

        private const float WagSmile = 0.30f;
        private const float WagLaugh = 1f;
        private const float WagReleaseTime = 0.56f; // smooth decay to zero after release
        private const float WagIdleGrace = 0.10f;   // no decay inside the grace window
        private const float WagIdleSpan = .64f;    // idle seconds that decay the wag to zero

        private const float PreviewLaughDelay = 0.36f;
        private const float PreviewLaughHold = 0.65f;
        private const float PreviewSmileHold = 1.8f;
        private const float PreviewTotal = PreviewLaughDelay + PreviewLaughHold + PreviewSmileHold;

        private const float NoSmile = -1f;

        private bool _touching;
        private float _lastMoveTime;
        private float _streakStart;
        private float _streakDistance;
        private float _smileSince = NoSmile;
        private float _smileUntil;
        private float _laughUntil;
        private bool _hasPulse;
        private float _lastPulseTime = float.NegativeInfinity;
        private float _pulseDistance;
        private bool _releasing;
        private float _releaseStart;
        private float _releaseFrom;
        private bool _previewing;
        private float _previewStart;

        /// <summary>Starts a real contact, interrupting any button preview.</summary>
        public void Begin(float now)
        {
            if (_touching) return;
            if (IsPreviewRunning(now)) _smileUntil = now + SmileLinger;
            _previewing = false;
            _touching = true;
            _lastMoveTime = now;
            _streakStart = now;
            _streakDistance = 0f;
            _pulseDistance = 0f;
            _hasPulse = false;
            _lastPulseTime = float.NegativeInfinity;
            _smileSince = NoSmile;
            _laughUntil = now;
            _releasing = false;
            _releaseStart = 0f;
            _releaseFrom = 0f;
            // _smileUntil is deliberately untouched: a silent re-touch must not extend the smile.
        }

        /// <summary>
        /// Feeds one movement event. Returns true when a laugh pulse starts or is renewed.
        /// Zero, negative or non-finite distance counts as holding still and changes nothing.
        /// </summary>
        public bool Stroke(float now, float distance)
        {
            if (!_touching || IsPreviewRunning(now))
            {
                return false;
            }

            float step = NormalizeStep(distance);
            if (step <= 0f)
            {
                return false;
            }

            if (now - _lastMoveTime > StreakGap)
            {
                DropPartialStreak(now);
            }

            if (_streakDistance == 0) _streakStart = now;
            if (now >= _smileUntil) _smileSince = NoSmile;
            _streakDistance += step;
            _pulseDistance += step;
            _lastMoveTime = now;

            float streakAge = now - _streakStart;
            if (IsSmileEarned(streakAge))
            {
                if (_smileSince < 0f)
                {
                    _smileSince = now;
                }

                _smileUntil = now + SmileLinger;
            }

            if (now < _laughUntil)
            {
                _laughUntil = now + LaughLife;
                _smileUntil = _laughUntil + SmileLinger;
            }
            return TryPulse(now, streakAge);
        }

        /// <summary>
        /// Lifts the contact. An active smile lingers 1.8s, then fades to calm.
        /// A calm face at lift stays calm; the wag starts its smooth release.
        /// </summary>
        public void End(float now)
        {
            if (!_touching || IsPreviewRunning(now))
            {
                return;
            }

            AffectionFace face = Face(now);
            _releaseFrom = CurrentWag(now, face);

            _smileUntil = face == AffectionFace.Calm ? now : now + SmileLinger;
            _laughUntil = now;

            _touching = false;
            _releaseStart = now;
            _releasing = true;
        }

        /// <summary>
        /// Plays a scripted preview: immediate smile, laugh after 0.36s, laugh held 0.65s,
        /// then smile for 1.8s. Never creates touch state. Rejects a second concurrent preview.
        /// </summary>
        public bool Preview(float now)
        {
            if (_touching || IsPreviewRunning(now))
            {
                return false;
            }

            _previewing = true;
            _previewStart = now;
            _smileUntil = now;
            _releaseFrom = 0;
            return true;
        }

        /// <summary>Hard interrupt of every state, including a running preview.</summary>
        public void Reset()
        {
            _touching = false;
            _lastMoveTime = 0f;
            _streakStart = 0f;
            _streakDistance = 0f;
            _smileSince = NoSmile;
            _smileUntil = 0f;
            _laughUntil = 0f;
            _hasPulse = false;
            _lastPulseTime = float.NegativeInfinity;
            _pulseDistance = 0f;
            _releasing = false;
            _releaseStart = 0f;
            _releaseFrom = 0f;
            _previewing = false;
            _previewStart = 0f;
        }

        /// <summary>Face shown at the given time.</summary>
        public AffectionFace Face(float now)
        {
            if (IsPreviewRunning(now))
            {
                return PreviewFace(now);
            }

            if (now < _laughUntil)
            {
                return AffectionFace.Laugh;
            }

            if (now < _smileUntil)
            {
                return AffectionFace.Smile;
            }

            return AffectionFace.Calm;
        }

        /// <summary>Wag amplitude in 0..1 at the given time.</summary>
        public float Wag(float now)
        {
            if (IsPreviewRunning(now))
            {
                float age = now - _previewStart;
                if (age < PreviewLaughDelay) return WagSmile;
                if (age >= PreviewLaughDelay + PreviewLaughHold) return 0;
                return Decay(1, (age - PreviewLaughDelay - .12f) / .64f);
            }

            if (_releasing)
            {
                float elapsed = now - _releaseStart;
                if (elapsed <= 0f)
                {
                    return _releaseFrom;
                }

                if (elapsed >= WagReleaseTime)
                {
                    return 0f;
                }

                return Decay(_releaseFrom, elapsed / WagReleaseTime);
            }

            if (!_touching)
            {
                return 0f;
            }

            return CurrentWag(now, Face(now));
        }


        private static float NormalizeStep(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            {
                return 0f;
            }

            return Math.Min(distance, MaxStepDistance);
        }

        private void DropPartialStreak(float now)
        {
            _streakStart = now;
            _streakDistance = 0f;
            _pulseDistance = 0f;
            _smileSince = NoSmile; // the smile has to become visible again
        }

        private bool IsSmileEarned(float streakAge)
        {
            return _streakDistance >= SmileDistance && streakAge >= SmileTime;
        }

        private bool TryPulse(float now, float streakAge)
        {
            if (_smileSince < 0f || now - _smileSince < SmileVisibleTime)
            {
                return false;
            }

            if (streakAge < LaughTime || _streakDistance < LaughDistance)
            {
                return false;
            }

            if (_hasPulse)
            {
                if (now - _lastPulseTime < PulseGapTime)
                {
                    return false;
                }

                if (_pulseDistance < PulseGapDistance)
                {
                    return false;
                }
            }

            _hasPulse = true;
            _lastPulseTime = now;
            _pulseDistance = 0f;
            _laughUntil = now + LaughLife;
            _smileUntil = _laughUntil + SmileLinger;
            return true;
        }

        private float CurrentWag(float now, AffectionFace face)
        {
            if (_streakDistance <= 0f)
            {
                return 0f;
            }

            float peak = face == AffectionFace.Laugh ? WagLaugh
                : face == AffectionFace.Smile ? WagSmile
                : 0f;

            if (peak <= 0f)
            {
                return 0f;
            }

            float idle = now - _lastMoveTime;
            if (idle <= WagIdleGrace)
            {
                return peak;
            }

            return Decay(peak, (idle - WagIdleGrace) / WagIdleSpan);
        }

        private static float Decay(float peak, float normalized)
        {
            float remaining = 1 - Math.Max(0, Math.Min(1, normalized));
            return peak * remaining * remaining;
        }

        private bool IsPreviewRunning(float now)
        {
            return _previewing && now >= _previewStart && now - _previewStart < PreviewTotal;
        }

        private AffectionFace PreviewFace(float now)
        {
            float elapsed = now - _previewStart;

            if (elapsed >= PreviewTotal)
            {
                return AffectionFace.Calm;
            }

            if (elapsed < PreviewLaughDelay)
            {
                return AffectionFace.Smile;
            }

            if (elapsed < PreviewLaughDelay + PreviewLaughHold)
            {
                return AffectionFace.Laugh;
            }

            return AffectionFace.Smile;
        }
    }
}
