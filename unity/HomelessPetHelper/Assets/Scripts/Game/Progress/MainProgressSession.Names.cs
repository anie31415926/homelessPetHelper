using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;

namespace HomelessPetHelper.Game.Progress
{
    public sealed partial class MainProgressSession
    {
        private readonly Dictionary<Guid, string> _customNames = new Dictionary<Guid, string>();
        internal const int MaximumAnimalNameLength = 8;
        public string GetCustomName(Guid animalId) =>
            _customNames.TryGetValue(animalId, out var name) ? name : null;

        internal string DefaultNameKey(StationAnimalIntake animal)
        {
            Guid id = _requestsByOuting[animal.OutingId];
            Session.Requests.TryGet(id, out var request);
            // Request order is persisted, including resolved requests no longer in the station.
            bool first = Session.Requests.Requests.First(item => item.TemplateId == request.TemplateId &&
                item.PointId == request.PointId).RequestId == id;
            if (first)
            {
                if (request.TemplateId == "ordinary" && request.PointId == "parking") return "petName.chestnut";
                if (request.TemplateId == "ordinary-dog" && request.PointId == "yard") return "petName.wheat";
                if (animal.AnimalDefinitionId == StationArtwork.AffectionDefinitionId) return "petName.doudou";
                if (request.TemplateId == "treehouse-kitten" && request.PointId == "treehouse")
                    return "petName.pinecone";
            }
            return "petName.pool." + (id.ToByteArray()[0] % 8);
        }

        internal static bool IsValidAnimalName(string name) =>
            name != null && name.Length > 0 && name.Length <= MaximumAnimalNameLength &&
            name.Any(character => !char.IsDigit(character)) &&
            name.All(character => character >= '0' && character <= '9' ||
                character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z' ||
                character >= 0x4E00 && character <= 0x9FFF);

        public bool TryRenameResident(Guid animalId, string name)
        {
            if (!CanContinue || Fields.HasActive || Session.Station.IsSuspended ||
                (Session.Flow.Stage != RescueStage.Station && Session.Flow.Stage != RescueStage.Care) ||
                !Session.Station.TryGetAnimal(animalId, out _) || !IsValidAnimalName(name)) return false;
            if (GetCustomName(animalId) == name) return true;
            var draft = Capture();
            draft.Residents.Single(item => item.Current.Id == animalId.ToString("N")).CustomName = name;
            if (!TryCommitStationDraft(draft)) return false;
            _customNames[animalId] = name;
            return true;
        }
    }
}
