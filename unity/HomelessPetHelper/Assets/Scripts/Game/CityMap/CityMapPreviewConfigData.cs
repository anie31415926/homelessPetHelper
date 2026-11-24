using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Game.CityMap
{
    // Authoring DTO only. Validated definitions and fresh state are built by CityMapPreviewConfig.
    [Serializable]
    internal sealed class CityMapPreviewConfigData
    {
        public int schemaVersion = 0;
        public string backgroundId = string.Empty;
        public string[] regionIds = Array.Empty<string>();
        public string initialPointId = string.Empty;
        public int initialCapacity = -1;
        public int minutesPerDay = 0;
        public int seed = -1;
        public TimePolicy timePolicy = null;
        public Point[] points = Array.Empty<Point>();
        public Template[] requestTemplates = Array.Empty<Template>();
        public RescueEncounter[] rescueEncounters = Array.Empty<RescueEncounter>();
        public Request[] initialRequests = Array.Empty<Request>();
        public RequestDispatch dispatch = null;

        [Serializable]
        internal sealed class RequestDispatch
        {
            public bool enabled;
            public int fullIntervalMinutes, lowIntervalMinutes, highIntervalMinutes;
            public int highCapacityThreshold, lifetimeMinutes;
            public Request[] candidates = Array.Empty<Request>();
        }

        [Serializable]
        internal sealed class RescueEncounter
        {
            public string templateId = string.Empty, animalDefinitionId = string.Empty, encounterId = string.Empty;
            public string rescueProfile = string.Empty;
            public bool dogArtwork, knownHealthy, isHungry, acceptsTouch, acceptsWash, acceptsGroom;
            public Disposition initialDisposition, rescuedDisposition;
            public int dirtiness, looseFur, washMinutes, groomMinutes;
        }

        [Serializable]
        internal sealed class Disposition
        {
            public AnimalEmotion emotion;
            public AnimalStress stress;
            public AnimalBehavior behavior;
        }

        [Serializable]
        internal sealed class TimePolicy
        {
            public long explorationMinutes = 0;
            public long openingMinutes = 0;
            public long negotiationMinutes = 0;
            public long handoverMinutes = 0;
        }

        [Serializable]
        internal sealed class Point
        {
            public string id = string.Empty;
            public string nameKey = string.Empty;
            public string detailKey = string.Empty;
            public string regionId = string.Empty;
            public bool openByDefault = false;
            public string[] sceneTemplateIds = Array.Empty<string>();
        }

        [Serializable]
        internal sealed class Template
        {
            public string id = string.Empty;
            public string textKey = string.Empty;
            public string animalDefinitionId = string.Empty;
            public string[] allowedPointIds = Array.Empty<string>();
        }

        [Serializable]
        internal sealed class Request
        {
            public string templateId = string.Empty;
            public string pointId = string.Empty;
            public int templateVersion = 0;
            public bool hasDirectReception = false;
            public Reunion reunion = null;
        }

        [Serializable]
        internal sealed class Reunion
        {
            public string ownerId = string.Empty;
            public string receivingArrangementId = string.Empty;
        }
    }
}
