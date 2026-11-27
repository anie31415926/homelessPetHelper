using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station.UI.Affection
{
    public sealed class PetAffectionMood
    {
        private double _smileUntil;

        internal void Enjoy(double now) => _smileUntil = now + 1.8;

        internal AffectionFace Face(AnimalDisposition disposition, double now, bool showRecentTouch)
        {
            if (disposition?.Stress == AnimalStress.Acute || disposition?.Emotion == AnimalEmotion.Irritated)
                return AffectionFace.Calm;
            return disposition?.Emotion == AnimalEmotion.Happy || showRecentTouch && now < _smileUntil
                ? AffectionFace.Smile : AffectionFace.Calm;
        }
    }
}
