namespace DesktopCalendar.App.ViewModels;

public sealed class EventEmojiOption(string emoji, string name, string category) : ObservableObject
{
    private bool _isSelected;

    public string Emoji { get; } = emoji;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public string AccessibleName => $"{Name} 이모지";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public static class EventEmojiCatalog
{
    public const string AllCategory = "전체";

    public static IReadOnlyList<string> Categories { get; } =
        [AllCategory, "추천", "공부", "업무", "생활", "건강", "약속", "여행"];

    public static IReadOnlyList<EventEmojiOption> CreateOptions() =>
    [
        new("⭐", "중요", "추천"), new("📌", "핀", "추천"), new("✅", "체크", "추천"), new("🔔", "알림", "추천"),
        new("🗓️", "달력", "추천"), new("💡", "아이디어", "추천"), new("🎯", "목표", "추천"), new("⏰", "시간", "추천"),

        new("📚", "책", "공부"), new("📖", "독서", "공부"), new("✏️", "연필", "공부"), new("📝", "필기", "공부"),
        new("🎓", "학습", "공부"), new("🧠", "집중", "공부"), new("🔬", "연구", "공부"), new("💻", "코딩", "공부"),

        new("💼", "업무", "업무"), new("📊", "보고서", "업무"), new("📈", "성과", "업무"), new("🗂️", "자료 정리", "업무"),
        new("📎", "문서", "업무"), new("🤝", "협업", "업무"), new("👨‍💻", "개발", "업무"), new("🏢", "회사", "업무"),

        new("🏠", "집", "생활"), new("🧹", "청소", "생활"), new("🛒", "장보기", "생활"), new("🍽️", "식사", "생활"),
        new("🧺", "빨래", "생활"), new("🚗", "운전", "생활"), new("🔧", "수리", "생활"), new("💳", "결제", "생활"),

        new("🏃", "달리기", "건강"), new("🏋️", "운동", "건강"), new("🧘", "명상", "건강"), new("🩺", "병원", "건강"),
        new("💊", "약", "건강"), new("🥗", "건강식", "건강"), new("😴", "수면", "건강"), new("💧", "물", "건강"),

        new("🎉", "파티", "약속"), new("🎂", "생일", "약속"), new("☕", "커피", "약속"), new("🍿", "영화", "약속"),
        new("❤️", "소중한 약속", "약속"), new("👥", "모임", "약속"), new("🎁", "선물", "약속"), new("📞", "전화", "약속"),

        new("✈️", "비행", "여행"), new("🚆", "기차", "여행"), new("🚌", "버스", "여행"), new("🏖️", "휴가", "여행"),
        new("🏕️", "캠핑", "여행"), new("🗺️", "여행 계획", "여행"), new("📷", "사진", "여행"), new("🧳", "짐", "여행")
    ];
}
