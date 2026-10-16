using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace RaceDay.Api.Tests;
public class ApiTests
{
    private const string Password = "RaceDay!2026";
    private static object Registration(string email="runner@example.com", string role="Participant") => new { fullName="Test User", email, password=Password, role };
    private static object EventBody(string name="City Run") => new { name, discipline="Running", eventDate=DateTime.Now.AddDays(10), location="Johannesburg", description="Community road race" };
    private static object CategoryBody(int capacity=100) => new { name="10km", distanceKm=10, maxParticipants=capacity, entryFee=100 };
    private static object ResultBody() => new { finishTime="00:48:32", overallPosition=5, categoryPosition=2, status="Finished" };
    private static async Task<int> Id(HttpResponseMessage response,string field)
    { var json=await response.Content.ReadFromJsonAsync<JsonElement>(); return json.GetProperty(field).GetInt32(); }
    private static async Task<int> RegisterLogin(HttpClient client,string role="Participant",string email="runner@example.com")
    {
        var response=await client.PostAsJsonAsync("/api/auth/register",Registration(email,role));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var id=await Id(response,"userId");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/login",new { email,password=Password })).StatusCode);
        return id;
    }
    private static async Task<(int Event,int Category)> SetupEvent(HttpClient organiser,int capacity=100)
    {
        var response=await organiser.PostAsJsonAsync("/api/events",EventBody());
        Assert.Equal(HttpStatusCode.Created,response.StatusCode); var e=await Id(response,"eventId");
        response=await organiser.PostAsJsonAsync($"/api/events/{e}/categories",CategoryBody(capacity));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode); return (e,await Id(response,"categoryId"));
    }
    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_ValidRole_HashesPasswordAndLoginStoresSession(string role)
    {
        using var f=new ApiFactory(); using var c=f.Client(); var id=await RegisterLogin(c,role);
        var profile=await c.GetAsync($"/api/users/{id}"); Assert.Equal(HttpStatusCode.OK,profile.StatusCode);
        Assert.DoesNotContain("password",await profile.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
        await f.Database(async db => { var u=await db.Users.SingleAsync(); Assert.NotEqual(Password,u.PasswordHash); Assert.True(BCrypt.Net.BCrypt.Verify(Password,u.PasswordHash)); });
    }
    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/auth/register",Registration("RUNNER@example.com"))).StatusCode);
    }
    [Theory]
    [InlineData("Admin","Test","valid@example.com","Password123")]
    [InlineData("Participant","","valid@example.com","Password123")]
    [InlineData("Participant","Test","bad-email","Password123")]
    [InlineData("Participant","Test","valid@example.com","short")]
    public async Task Register_InvalidInput_ReturnsBadRequest(string role,string name,string email,string password)
    {
        using var f=new ApiFactory(); using var c=f.Client();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/register",new { fullName=name,email,password,role })).StatusCode);
    }
    [Theory]
    [InlineData("runner@example.com","wrong")]
    [InlineData("unknown@example.com","RaceDay!2026")]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized(string email,string password)
    {
        using var f=new ApiFactory(); using var c=f.Client(); await c.PostAsJsonAsync("/api/auth/register",Registration());
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/auth/login",new { email,password })).StatusCode);
    }
    [Fact]
    public async Task Login_Part1PlaceholderHash_ReturnsUnauthorizedWithoutException()
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c);
        await f.Database(async db => { (await db.Users.SingleAsync()).PasswordHash="HASH_PLACEHOLDER_1"; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/auth/login",new { email="runner@example.com",password=Password })).StatusCode);
    }
    [Fact]
    public async Task Logout_LoggedIn_RejectsSubsequentProtectedRequest()
    {
        using var f=new ApiFactory(); using var c=f.Client(); var id=await RegisterLogin(c);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsync("/api/auth/logout",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync($"/api/users/{id}")).StatusCode);
    }
    [Theory]
    [InlineData("GET","/api/users/1",null)]
    [InlineData("PUT","/api/users/1","profile")]
    [InlineData("POST","/api/events","event")]
    [InlineData("PUT","/api/events/1","event")]
    [InlineData("DELETE","/api/events/1",null)]
    [InlineData("GET","/api/events/1/enrolments",null)]
    [InlineData("POST","/api/events/1/categories","category")]
    [InlineData("PUT","/api/categories/1","category")]
    [InlineData("DELETE","/api/categories/1",null)]
    [InlineData("POST","/api/categories/1/enrolments",null)]
    [InlineData("GET","/api/users/1/enrolments",null)]
    [InlineData("DELETE","/api/enrolments/1",null)]
    [InlineData("POST","/api/enrolments/1/results","result")]
    [InlineData("PUT","/api/results/1","result")]
    public async Task ProtectedEndpoint_NoSession_ReturnsUnauthorized(string method,string path,string? body)
    {
        using var f=new ApiFactory(); using var c=f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,(await Send(c,method,path,body)).StatusCode);
    }
    private static Task<HttpResponseMessage> Send(HttpClient c,string method,string path,string? body)
    {
        var r=new HttpRequestMessage(new HttpMethod(method),path);
        object? value=body switch { "event"=>EventBody(),"category"=>CategoryBody(),"result"=>ResultBody(),"profile"=>new { fullName="Test",email="test@example.com" },_=>null };
        if(value!=null) r.Content=JsonContent.Create(value); return c.SendAsync(r);
    }
    [Theory]
    [InlineData("Participant","POST","/api/events","event")]
    [InlineData("Participant","PUT","/api/events/1","event")]
    [InlineData("Participant","DELETE","/api/events/1",null)]
    [InlineData("Participant","GET","/api/events/1/enrolments",null)]
    [InlineData("Participant","POST","/api/events/1/categories","category")]
    [InlineData("Participant","PUT","/api/categories/1","category")]
    [InlineData("Participant","DELETE","/api/categories/1",null)]
    [InlineData("Participant","POST","/api/enrolments/1/results","result")]
    [InlineData("Participant","PUT","/api/results/1","result")]
    [InlineData("Organiser","POST","/api/categories/1/enrolments",null)]
    [InlineData("Organiser","GET","/api/users/1/enrolments",null)]
    [InlineData("Organiser","DELETE","/api/enrolments/1",null)]
    public async Task ProtectedEndpoint_WrongRole_ReturnsForbidden(string role,string method,string path,string? body)
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c,role);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(c,method,path,body)).StatusCode);
    }
    [Fact]
    public async Task Profile_OwnerCanUpdate_OtherUserCannotReadOrUpdate()
    {
        using var f=new ApiFactory(); using var c=f.Client(); var id=await RegisterLogin(c);
        Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/users/{id}",new { fullName="Updated",email="new@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/users/{id+1}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PutAsJsonAsync($"/api/users/{id+1}",new { fullName="Updated",email="new@example.com" })).StatusCode);
    }
    [Fact]
    public async Task Events_OwnerCanCreateUpdateDelete_PublicCanBrowse()
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c,"Organiser"); var (e,cat)=await SetupEvent(c);
        Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/events/{e}",EventBody("Updated"))).StatusCode);
        using var anon=f.Client(); Assert.Equal(HttpStatusCode.OK,(await anon.GetAsync($"/api/events/{e}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anon.GetAsync("/api/events?discipline=Running&location=Johannesburg")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"/api/events/{e}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/events/{e}")).StatusCode);
    }
    [Fact]
    public async Task EventsAndCategories_OtherOrganiser_ReturnsForbidden()
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c,"Organiser"); var (e,cat)=await SetupEvent(c);
        using var other=f.Client(); await RegisterLogin(other,"Organiser","other@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PutAsJsonAsync($"/api/events/{e}",EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.DeleteAsync($"/api/events/{e}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync($"/api/events/{e}/categories",CategoryBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PutAsJsonAsync($"/api/categories/{cat}",CategoryBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.DeleteAsync($"/api/categories/{cat}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync($"/api/events/{e}/enrolments")).StatusCode);
    }
    [Fact]
    public async Task Categories_OwnerCanUpdateAndDelete()
    {
        using var f=new ApiFactory(); using var c=f.Client(); await RegisterLogin(c,"Organiser"); var (e,cat)=await SetupEvent(c);
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync($"/api/events/{e}/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/categories/{cat}",CategoryBody(50))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"/api/categories/{cat}")).StatusCode);
    }
    [Fact]
    public async Task Enrolment_ValidRequest_StoresParticipantCategoryEvent_RejectsDuplicateAndProtectsHistory()
    {
        using var f=new ApiFactory(); using var owner=f.Client(); await RegisterLogin(owner,"Organiser"); var (e,cat)=await SetupEvent(owner);
        using var c=f.Client(); var user=await RegisterLogin(c,"Participant","p@example.com");
        var response=await c.PostAsync($"/api/categories/{cat}/enrolments",null); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var entry=await Id(response,"enrolmentId");
        await f.Database(async db => { var row=await db.Enrolments.Include(x=>x.Category).SingleAsync(); Assert.Equal(user,row.ParticipantId); Assert.Equal(cat,row.CategoryId); Assert.Equal(e,row.Category!.EventId); });
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsync($"/api/categories/{cat}/enrolments",null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync($"/api/events/{e}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.DeleteAsync($"/api/events/{e}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.DeleteAsync($"/api/categories/{cat}")).StatusCode);
        using var other=f.Client(); await RegisterLogin(other,"Participant","other@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.DeleteAsync($"/api/enrolments/{entry}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync($"/api/users/{user}/enrolments")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"/api/enrolments/{entry}")).StatusCode);
        await f.Database(async db => Assert.Equal("Cancelled",(await db.Enrolments.SingleAsync()).Status));
    }
    [Fact]
    public async Task Enrolment_MissingFullAndPastCategory_Rejected()
    {
        using var f=new ApiFactory(); using var owner=f.Client(); await RegisterLogin(owner,"Organiser"); var (e,cat)=await SetupEvent(owner,1);
        using var c=f.Client(); await RegisterLogin(c,"Participant","p@example.com");
        Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsync("/api/categories/999/enrolments",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,(await c.PostAsync($"/api/categories/{cat}/enrolments",null)).StatusCode);
        using var other=f.Client(); await RegisterLogin(other,"Participant","other@example.com");
        Assert.Equal(HttpStatusCode.Conflict,(await other.PostAsync($"/api/categories/{cat}/enrolments",null)).StatusCode);
        await f.Database(async db => { (await db.Events.FindAsync(e))!.EventDate=DateTime.Now.AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict,(await other.PostAsync($"/api/categories/{cat}/enrolments",null)).StatusCode);
    }
    [Fact]
    public async Task Results_AfterEvent_OwnerCapturesAndCorrects_ParticipantViewsOwnHistory()
    {
        using var f=new ApiFactory(); using var owner=f.Client(); await RegisterLogin(owner,"Organiser"); var (e,cat)=await SetupEvent(owner);
        using var c=f.Client(); var user=await RegisterLogin(c,"Participant","p@example.com");
        var entry=await Id(await c.PostAsync($"/api/categories/{cat}/enrolments",null),"enrolmentId");
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PostAsJsonAsync($"/api/enrolments/{entry}/results",ResultBody())).StatusCode);
        await f.Database(async db => { (await db.Events.FindAsync(e))!.EventDate=DateTime.Now.AddDays(-1); await db.SaveChangesAsync(); });
        var response=await owner.PostAsJsonAsync($"/api/enrolments/{entry}/results",ResultBody()); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var result=await Id(response,"resultId");
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PostAsJsonAsync($"/api/enrolments/{entry}/results",ResultBody())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await owner.PutAsJsonAsync($"/api/results/{result}",ResultBody())).StatusCode);
        var history=await c.GetFromJsonAsync<JsonElement>($"/api/users/{user}/enrolments");
        Assert.Equal(result,history[0].GetProperty("result").GetProperty("resultId").GetInt32());
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync($"/api/categories/{cat}/results")).StatusCode);
        using var other=f.Client(); await RegisterLogin(other,"Organiser","other@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync($"/api/enrolments/{entry}/results",ResultBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PutAsJsonAsync($"/api/results/{result}",ResultBody())).StatusCode);
    }
    [Theory]
    [InlineData("/api/events/999")]
    [InlineData("/api/events/999/categories")]
    [InlineData("/api/categories/999/results")]
    public async Task PublicRead_MissingRecord_ReturnsNotFound(string path)
    {
        using var f=new ApiFactory(); using var c=f.Client(); Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task Swagger_AllPlannedRoutes_HaveDescriptionsAndResponseSchemas()
    {
        using var f=new ApiFactory(); using var c=f.Client(); var doc=await c.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths=doc.GetProperty("paths"); int operations=0;
        foreach(var path in paths.EnumerateObject()) foreach(var op in path.Value.EnumerateObject())
        { Assert.False(string.IsNullOrWhiteSpace(op.Value.GetProperty("summary").GetString())); Assert.True(op.Value.GetProperty("responses").EnumerateObject().Any()); operations++; }
        Assert.Equal(21,operations);
    }
}
