namespace PlanningPulse.Web;

public class AppState
{
    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string TenantName { get; private set; } = string.Empty;
    public string PageTitle { get; set; } = "Dashboard";
    public string AccessToken { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }

    public string UserInitials => string.IsNullOrEmpty(UserName)
        ? "?"
        : string.Concat(UserName.Split(' ').Take(2).Select(w => w[0])).ToUpper();

    public event Action? OnChange;

    public void Login(string userName, string tenantName, string accessToken, Guid tenantId)
    {
        IsAuthenticated = true;
        UserName = userName;
        TenantName = tenantName;
        AccessToken = accessToken;
        TenantId = tenantId;
        NotifyStateChanged();
    }

    public void Logout()
    {
        IsAuthenticated = false;
        UserName = string.Empty;
        TenantName = string.Empty;
        AccessToken = string.Empty;
        TenantId = Guid.Empty;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
