using Henchman.Models;

namespace Henchman.Data;

public static class NpcDatabase
{
    public enum StarterCity
    {
        LimsaLominsa = 1,
        Gridania,
        Uldah
    }

    public static Dictionary<StarterCity, NpcData> RetainerVocates = new()
                                                                     {
                                                                             {
                                                                                     StarterCity.Gridania, new NpcData
                                                                                                           {
                                                                                                                   Name                   = "Parnell",
                                                                                                                   BaseId                 = 1000233,
                                                                                                                   AetheryteId            = 2,
                                                                                                                   AetheryteTerritoryId   = 132,
                                                                                                                   TerritoryId            = 133,
                                                                                                                   InteractablePosition   = new Vector3(168.00f, 15.5f, -94f),
                                                                                                                   ZoneTransitionPosition = new Vector3(101f, 4.93f, 14f)
                                                                                                           }
                                                                             },
                                                                             {
                                                                                     StarterCity.Uldah, new NpcData
                                                                                                        {
                                                                                                                Name                 = "Chachabi",
                                                                                                                BaseId               = 1001963,
                                                                                                                AetheryteId          = 9,
                                                                                                                AetheryteTerritoryId = 130,
                                                                                                                TerritoryId          = 131, // Steps of Thal
                                                                                                                InteractablePosition =
                                                                                                                        new Vector3(107.69f, 4.2f, -73.42f),
                                                                                                                ZoneTransitionPosition =
                                                                                                                        new Vector3(101.57f, 4f, -104.66f)
                                                                                                        }
                                                                             },
                                                                             {
                                                                                     StarterCity.LimsaLominsa, new NpcData
                                                                                                               {
                                                                                                                       Name                   = "Frydwyb",
                                                                                                                       BaseId                 = 1003275,
                                                                                                                       AetheryteId            = 8,
                                                                                                                       TerritoryId            = 129,
                                                                                                                       InteractablePosition   = new Vector3(-146.17f, 18.21f, 16.89f),
                                                                                                                       ZoneTransitionPosition = null
                                                                                                               }
                                                                             }
                                                                     };


    public static Dictionary<StarterCity, NpcData> BeginnerDoWDoMVendor = new()
                                                                          {
                                                                                  {
                                                                                          StarterCity.Gridania, new NpcData
                                                                                                                {
                                                                                                                        Name                 = "Geraint",
                                                                                                                        BaseId               = 1000217,
                                                                                                                        AetheryteId          = 2,
                                                                                                                        AetheryteTerritoryId = 132,
                                                                                                                        TerritoryId          = 133,
                                                                                                                        InteractablePosition =
                                                                                                                                new Vector3(168.14f, 15.7f, -73.98f),
                                                                                                                        ZoneTransitionPosition = new Vector3(101f, 4.93f, 14f)
                                                                                                                }
                                                                                  },
                                                                                  {
                                                                                          StarterCity.Uldah, new NpcData
                                                                                                             {
                                                                                                                     Name                 = "Jealous Juggernaut",
                                                                                                                     BaseId               = 1000217,
                                                                                                                     AetheryteId          = 9,
                                                                                                                     AetheryteTerritoryId = 130,
                                                                                                                     TerritoryId          = 131, // Steps of Thal
                                                                                                                     InteractablePosition = new Vector3(137.97f, 4f, -9.6f),
                                                                                                                     ZoneTransitionPosition =
                                                                                                                             new Vector3(101.57f, 4f, -104.66f)
                                                                                                             }
                                                                                  },
                                                                                  {
                                                                                          StarterCity.LimsaLominsa, new NpcData
                                                                                                                    {
                                                                                                                            Name        = "Faezghim",
                                                                                                                            BaseId      = 1001205,
                                                                                                                            AetheryteId = 8,
                                                                                                                            TerritoryId = 129,
                                                                                                                            InteractablePosition =
                                                                                                                                    new Vector3(-236.33f, 16.2f, 40.45f),
                                                                                                                            ZoneTransitionPosition = null
                                                                                                                    }
                                                                                  }
                                                                          };

    public static Dictionary<StarterCity, NpcData> BeginnerDoLVendor = new()
                                                                       {
                                                                               {
                                                                                       StarterCity.Gridania, new NpcData
                                                                                                             {
                                                                                                                     Name                   = "Admiranda",
                                                                                                                     BaseId                 = 1000218,
                                                                                                                     AetheryteId            = 2,
                                                                                                                     AetheryteTerritoryId   = 132,
                                                                                                                     TerritoryId            = 133,
                                                                                                                     InteractablePosition   = new Vector3(162.75f, 15.7f, -58.83f),
                                                                                                                     ZoneTransitionPosition = new Vector3(101f, 4.93f, 14f)
                                                                                                             }
                                                                               },
                                                                               {
                                                                                       StarterCity.Uldah, new NpcData
                                                                                                          {
                                                                                                                  Name                   = "Yoyobasa",
                                                                                                                  BaseId                 = 1001973,
                                                                                                                  AetheryteId            = 9,
                                                                                                                  AetheryteTerritoryId   = 130,
                                                                                                                  TerritoryId            = 131, // Steps of Thal
                                                                                                                  InteractablePosition   = new Vector3(150.02f, 4f, 0.25f),
                                                                                                                  ZoneTransitionPosition = new Vector3(101.57f, 4f, -104.66f)
                                                                                                          }
                                                                               },
                                                                               {
                                                                                       StarterCity.LimsaLominsa, new NpcData
                                                                                                                 {
                                                                                                                         Name                   = "Syneyhil",
                                                                                                                         BaseId                 = 1003254,
                                                                                                                         AetheryteId            = 8,
                                                                                                                         TerritoryId            = 129,
                                                                                                                         InteractablePosition   = new Vector3(-246.66f, 16.2f, 40.09f),
                                                                                                                         ZoneTransitionPosition = null
                                                                                                                 }
                                                                               }
                                                                       };
}
