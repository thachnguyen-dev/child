namespace ChildGame
{
    /// <summary>The three difficulty levels every mini game has (design §6). The int values index save arrays.</summary>
    public enum Difficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>The protagonist's gender, chosen once at the first start (design §5).</summary>
    public enum Gender { Boy = 0, Girl = 1 }

    /// <summary>Supported languages (design §2).</summary>
    public enum Language { Vietnamese = 0, English = 1 }
}
