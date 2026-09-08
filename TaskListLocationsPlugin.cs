using BepInEx;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TMPro;

namespace Passass.TaskListLocations
{
    [BepInPlugin("com.passass.tasklistlocations", "TaskListLocations", "1.0.0")]
    [BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("xyz.drakia.tasklistfixes", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static ManualLogSource Log => Instance.Logger;
        private void Awake()
        {
            Instance = this;
            QuestLocation.InitQuests();
            NotesTaskPatch NotesTaskPatchInstance = new NotesTaskPatch();
            NotesTaskPatchInstance.Enable();

            if (Utils.NamespaceExists("DrakiaXYZ.TaskListFixes.Comparers"))
            {
                TestPatch testPatch = new TestPatch();
                testPatch.InitTypes(this);
                testPatch.Enable();
            }
        }
    };

    public class QuestLocation
    {
        private static Dictionary<string, QuestLocation> QuestLocations { get; set; } = null;

        public static void InitQuests()
        {
            try
            {

                string PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string QuestsPath = Path.Combine(PluginFolder, "quest_locations.json");
                string jsonString = File.ReadAllText(QuestsPath);

                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter>
                    {
                        new StringEnumConverter()
                    },
                    NullValueHandling = NullValueHandling.Ignore
                };

                QuestLocations = JsonConvert.DeserializeObject<Dictionary<string, QuestLocation>>(jsonString, settings);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo("FILE READING ERROR: " + ex.Message);
                QuestLocations = new Dictionary<string, QuestLocation>();
            }
        }

        public static QuestLocation FromQuestClass(EFT.Quests.Quest Quest)
        {
            if (QuestLocations.TryGetValue(Quest.Id, out QuestLocation value))
            {
                return value;
            }

            string existing_location_id = Quest.Template.LocationId;

            if (existing_location_id != "any")
            {
                QuestLocation questLocation = new QuestLocation
                {
                    Type = QuestType.Locations,
                    Locations = new List<string> { existing_location_id }
                };

                QuestLocations.Add(Quest.Id, questLocation);

                return questLocation;
            }

            return null;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum QuestType
        {
            [EnumMember(Value = "locations")]
            Locations,

            [EnumMember(Value = "location_excluding")]
            LocationExcluding,

            [EnumMember(Value = "find_in_raid")]
            FindInRaid,

            [EnumMember(Value = "stash")]
            Stash,

            [EnumMember(Value = "any_location")]
            AnyLocation,

            [EnumMember(Value = "many_location")]
            ManyLocation
        }

        [JsonProperty("type")]
        public QuestType Type { get; set; }

        [JsonProperty("locations")]
        public List<string> Locations { get; set; } = null;

        [JsonIgnore]
        public string LocalizedText { get; set; } = null;
        public string GetLocalized()
        {
            if (LocalizedText != null)
                return LocalizedText;

            switch (Type)
            {
                case QuestType.LocationExcluding:
                case QuestType.Locations:
                    if (Locations == null)
                        return "";
                    StringBuilder sb = new StringBuilder();
                    foreach (string location in Locations)
                    {
                        sb.Append(Utils.GetLocalizedText(location + " Name"));
                        sb.Append(", ");
                    }
                    string result = sb.ToString().TrimEnd(',', ' ');
                    if (Type == QuestType.LocationExcluding)
                        LocalizedText = (LocalizationManager.DefaultLanguage == "ru" ? "Все кроме " : "Any location except") + result;
                    else
                        LocalizedText = result;
                    break;
                case QuestType.FindInRaid:
                    LocalizedText = LocalizationManager.DefaultLanguage == "ru" ? "Найти в рейде" : "Found in raid";
                    break;
                case QuestType.Stash:
                    LocalizedText = Utils.Capitalize(Utils.GetLocalizedText("STASH"));
                    break;
                case QuestType.ManyLocation:
                    LocalizedText = LocalizationManager.DefaultLanguage == "ru" ? "Множество локаций" : "Many Locations";
                    break;
                case QuestType.AnyLocation:
                    LocalizedText = Utils.GetLocalizedText("any Name");
                    break;
            }
            return LocalizedText;
        }
    }

    class TestPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                QuestLocationComparer
                , "Compare"
            );
        }

        static Plugin PluginInstance;
        static Type QuestLocationComparer;

        static MethodInfo _stringLocalizedMethod;

        static Type TaskListFixesPlugin;
        static Type Settings;

        public void InitTypes(Plugin plugin)
        {
            PluginInstance = plugin;
            QuestLocationComparer = Utils.GetInternalType("DrakiaXYZ.TaskListFixes.Comparers.QuestLocationComparer");

            Settings = Utils.GetInternalType("DrakiaXYZ.TaskListFixes.Settings");
            TaskListFixesPlugin = Utils.GetInternalType("DrakiaXYZ.TaskListFixes.TaskListFixesPlugin");
            _stringLocalizedMethod = (MethodInfo)Utils.GetValueByPath(TaskListFixesPlugin, "_stringLocalizedMethod");
        }
        public static string Localized(string input)
        {
            return (string)_stringLocalizedMethod.Invoke(null, new object[] { input, null });
        }

        public static bool HandleNullOrEqualQuestCompare(EFT.Quests.Quest quest1, EFT.Quests.Quest quest2, out int result)
        {
            if (quest1 == quest2)
            {
                result = 0;
                return true;
            }

            if (quest1 == null)
            {
                result = -1;
                return true;
            }

            if (quest2 == null)
            {
                result = 1;
                return true;
            }

            result = 0;
            return false;
        }
        public static int SortCompare(
            string ___locationId,
            EFT.Quests.Quest quest1,
            EFT.Quests.Quest quest2)
        {
            if (HandleNullOrEqualQuestCompare(quest1, quest2, out int is_equal_result))
            {
                return is_equal_result;
            }

            QuestLocation location1 = QuestLocation.FromQuestClass(quest1);
            QuestLocation location2 = QuestLocation.FromQuestClass(quest2);

            // Проверяем, содержит ли квест искомую локацию
            bool quest1HasLocation = IsQuestMatchesLocation(quest1, location1, ___locationId);
            bool quest2HasLocation = IsQuestMatchesLocation(quest2, location2, ___locationId);

            if (quest1HasLocation != quest2HasLocation)
            {
                return quest1HasLocation ? 1 : -1;
            }

            return SortByOtherCriteria(quest1, quest2, location1, location2);
        }

        private static bool IsQuestMatchesLocation(
            EFT.Quests.Quest quest,
            QuestLocation location,
            string locationId)
        {
            if (location == null)
                return false;

            if (quest.Template.LocationId == locationId)
                return true;

            if (location.Type == QuestLocation.QuestType.Locations &&
                location.Locations != null &&
                location.Locations.Contains(locationId))
            {
                return true;
            }

            return false;
        }

        private static int SortByOtherCriteria(
            EFT.Quests.Quest quest1,
            EFT.Quests.Quest quest2,
            QuestLocation location1,
            QuestLocation location2)
        {
            if (location1 == null && location2 == null)
            {
                return SortByTraderNameTime(quest1, quest2);
            }

            if (location1 == null && location2 != null)
                return 1;
            if (location1 != null && location2 == null)
                return -1;

            int priority1 = GetLocationPriority(location1.Type);
            int priority2 = GetLocationPriority(location2.Type);

            if (priority1 != priority2)
                return priority1.CompareTo(priority2);

            if (location1.Type == location2.Type)
            {
                if (location1.Locations != null && location2.Locations != null &&
                    location1.Locations.Count > 0 && location2.Locations.Count > 0)
                {
                    string locName1 = Localized(location1.Locations[0] + " Name");
                    string locName2 = Localized(location2.Locations[0] + " Name");

                    int locCompare = string.CompareOrdinal(locName1, locName2);
                    if (locCompare != 0)
                        return locCompare;
                }

                return SortByTraderNameTime(quest1, quest2);
            }

            return string.CompareOrdinal(
                location1.GetLocalized(),
                location2.GetLocalized()
            );
        }

        private static int SortByTraderNameTime(EFT.Quests.Quest quest1, EFT.Quests.Quest quest2)
        {
            string traderId1 = quest1.Template.TraderId;
            string traderId2 = quest2.Template.TraderId;
            bool isGroupLocByTrader = (bool)Utils.GetValueByPath(Settings, "GroupLocByTrader.Value");
            bool isSubSortByName = (bool)Utils.GetValueByPath(Settings, "SubSortByName.Value");

            if (isGroupLocByTrader && traderId1 != traderId2)
            {
                string traderName1 = Localized(traderId1 + " Nickname");
                string traderName2 = Localized(traderId2 + " Nickname");
                return string.CompareOrdinal(traderName1, traderName2);
            }

            if (isSubSortByName)
            {
                string questName1 = Localized(quest1.Template.Id + " name");
                string questName2 = Localized(quest2.Template.Id + " name");
                if (questName1 != questName2)
                {
                    return string.CompareOrdinal(questName1, questName2);
                }
            }

            return quest1.StartTime.CompareTo(quest2.StartTime);
        }

        // Вспомогательный метод для определения приоритета типов локаций
        private static int GetLocationPriority(QuestLocation.QuestType type)
        {
            switch (type)
            {
                case QuestLocation.QuestType.Locations:
                case QuestLocation.QuestType.LocationExcluding:
                    return 0; // Наивысший приоритет
                case QuestLocation.QuestType.Stash:
                    return 1;
                case QuestLocation.QuestType.FindInRaid:
                    return 2;
                case QuestLocation.QuestType.ManyLocation:
                    return 3;
                case QuestLocation.QuestType.AnyLocation:
                    return 4;
                default:
                    return 5;
            }
        }

        [PatchPrefix]
        public static bool PatchPrefix(
            ref int __result
            , ref string ___locationId
            , EFT.Quests.Quest quest1
            , EFT.Quests.Quest quest2
        )
        {
            __result = SortCompare(
                ___locationId
                , quest1
                , quest2
            );
            return false;
        }
    }


    class NotesTaskPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(NotesTask), "Show");
        }

        [PatchPostfix]
        public static void PatchPostfix(
            NotesTask __instance,
            EFT.Quests.Quest quest,
            IEftSession session,
            InventoryController inventoryController,
            EFT.Quests.QuestController questController,
            NotesTaskDescriptionShort description,
            FavoriteQuestManager favoriteQuests,
            bool availability,
            ref TextMeshProUGUI ____locationLabel
        )
        {
            QuestLocation questLocation = QuestLocation.FromQuestClass(quest);
            if (questLocation != null)
            {
                ____locationLabel.text = questLocation.GetLocalized();
            }
        }
    }
}
