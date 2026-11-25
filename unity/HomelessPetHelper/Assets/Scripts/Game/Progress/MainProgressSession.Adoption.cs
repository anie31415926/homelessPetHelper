using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;

namespace HomelessPetHelper.Game.Progress
{
    public sealed partial class MainProgressSession
    {
        private readonly HashSet<Guid> _listed = new HashSet<Guid>();
        private MemorialData[] _memorials;
        internal int MemorialCount => _memorials.Length;
        internal MemorialData GetMemorial(int index) => _memorials[index];
        internal bool IsListed(Guid animalId) => _listed.Contains(animalId);
        internal static bool IsMemorialName(string name) => !string.IsNullOrWhiteSpace(name) &&
            name.Length <= 24 && !name.Any(char.IsControl);

        // Applications stay stable across reopening without storing candidate profiles.
        internal static int Applicant(Guid animalId, int index) =>
            (animalId.ToByteArray()[0] % 3 + index) % 3;

        internal string AdoptionBlock(Guid animalId)
        {
            if (!CanContinue || Fields.HasActive)
                return "adoption.unavailable";
            if (!Session.Station.TryGetCareSnapshot(animalId, out var care))
                return "adoption.unavailable";
            if (care.BasicAssessment == StationHealthAssessment.Unchecked)
                return "adoption.checkFirst";
            return Session.Flow.Stage == RescueStage.Station &&
                Session.Station.CanReleaseHealthyResident(Session.StationSessionId, animalId)
                ? null : "adoption.notReady";
        }

        public bool TryPublishAdoption(Guid animalId)
        {
            if (AdoptionBlock(animalId) != null || IsListed(animalId)) return false;
            var draft = Capture();
            draft.Residents.Single(item => item.Current.Id == animalId.ToString("N")).AdoptionListed = true;
            if (!TryCommitStationDraft(draft)) return false;
            _listed.Add(animalId);
            return true;
        }

        public bool TrySendForAdoption(Guid animalId, string name, int applicant)
        {
            if (AdoptionBlock(animalId) != null || !IsListed(animalId) || !IsMemorialName(name) ||
                (applicant != Applicant(animalId, 0) && applicant != Applicant(animalId, 1))) return false;
            var draft = Capture();
            var resident = draft.Residents.Single(item => item.Current.Id == animalId.ToString("N"));
            draft.Memorials = _memorials.Append(new MemorialData
            {
                RequestId = resident.RequestId, Name = name, Appearance = resident.Current.DefinitionId
            }).ToArray();
            draft.Residents = draft.Residents.Where(item => item != resident).ToArray();
            draft.Fields = new WorldFieldData
            {
                Locations = draft.Fields.Locations,
                Encounters = draft.Fields.Encounters.Where(item => item.RequestId != resident.RequestId).ToArray()
            };
            if (_economyRules != null && !draft.Economy.AdoptionRewarded)
            {
                draft.Economy.Balance = checked(draft.Economy.Balance + _economyRules.adoptionAid);
                draft.Economy.AdoptionRewarded = true;
            }
            if (!TryCommitStationDraft(draft)) return false;
            PublishEconomy(draft.Economy);
            if (!Session.Station.TryReleaseHealthyResident(Session.StationSessionId, animalId))
                throw new InvalidOperationException("Committed adoption eligibility changed on the gameplay thread.");
            Session.ForgetDepartedResident(animalId);
            Care.Forget(animalId);
            _requestsByOuting.Remove(Guid.ParseExact(resident.OutingId, "N"));
            Fields.ApplyArchivedEncounters(draft.Fields.Encounters);
            _listed.Remove(animalId);
            _memorials = draft.Memorials;
            return true;
        }

        private MainProgressData Capture()
        {
            var data = MainProgressCodec.Capture(Session, Care, _requestsByOuting, Fields.Capture(Session));
            data.Economy = CaptureEconomy();
            data.Dispatch = _dispatch.Capture();
            data.Memorials = _memorials;
            foreach (var resident in data.Residents)
                resident.AdoptionListed = IsListed(Guid.ParseExact(resident.Current.Id, "N"));
            return data;
        }

        private bool TryCommitStationDraft(MainProgressData draft)
        {
            if (_saves != null)
            {
                if (_saves.TryCommit(writer => writer.Set(_section, draft), out var error)) return true;
                PersistenceError = error;
                return false;
            }
            if (Validate(draft) == null) return true;
            PersistenceError = new SaveError(SaveErrorCode.InvalidSection, "Invalid station transaction checkpoint.");
            return false;
        }
    }
}
