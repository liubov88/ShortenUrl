var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(builder =>
    {
      builder.ConfigureServices(services =>
      {
        services.AddSingleton<ILiteDatabase, LiteDatabase>((sp) =>
        {
          var db = new LiteDatabase("local.db");
          var collection = db.GetCollection<NewShortLink>(BsonAutoId.Int32);
          collection.EnsureIndex(p => p.Link);
          collection.Upsert(new NewShortLink
          {
            Id = 1,
            Link = "https://www.google.com/",
          });
          return db;
        });
        services.AddRouting();
      })
      .Configure(app =>
      {
        app.UseRouting();
        app.UseEndpoints((endpoints) =>
        {
          endpoints.MapPost("/newUrl", HandleShortenUrl);
          endpoints.MapFallback(HandleUrl);
        });
      });
    })
    .Build();
await host.RunAsync();

static Task HandleUrl(HttpContext context)
{
  if (context.Request.Path == "/")
  {
    return context.Response.SendFileAsync("wwwroot/index.htm");
  }

  // Default to home page if no matching url.
  var redirect = "/";

  var db = context.RequestServices.GetService<ILiteDatabase>();
  var collection = db.GetCollection<NewShortLink>();

  var path = context.Request.Path.ToUriComponent().Trim('/');
  var id = NewShortLink.GetById(path);
  var entry = collection.Find(p => p.Id == id).SingleOrDefault();

  if (entry is not null)
  {
    redirect = entry.Link;
  }
  context.Response.Redirect(redirect);
  return Task.CompletedTask;
}



static Task PutResp(HttpContext context, int status, string response)
{
  context.Response.StatusCode = status;
  return context.Response.WriteAsync(response);
}

static Task HandleShortenUrl(HttpContext context)
{
  // Retrieve our dependencies
  var db = context.RequestServices.GetService<ILiteDatabase>();
  var collection = db.GetCollection<NewShortLink>(nameof(NewShortLink));

  // Perform basic form validation
  if (!context.Request.HasFormContentType || !context.Request.Form.ContainsKey("url"))
  {
    return PutResp(context, StatusCodes.Status400BadRequest, "Cannot process request.");
  }
  else
  {
    context.Request.Form.TryGetValue("url", out var formData);
    var requestedUrl = formData.ToString();

    // Test our URL
    if (!Uri.TryCreate(requestedUrl, UriKind.Absolute, out Uri result))
    {
      return PutResp(context, StatusCodes.Status400BadRequest, "general error");
    }

    var url = result.ToString();
    var entry = collection.Find(p => p.Link == url).FirstOrDefault();

    if (entry is null)
    {
      entry = new NewShortLink
      {
        Link = url
      };
      collection.Insert(entry);
    }

    var urlChunk = entry.GetNewUrl();
    var responseUri = $"{context.Request.Scheme}://{context.Request.Host}/{urlChunk}";
    context.Response.Redirect($"/#{responseUri}");
    return Task.CompletedTask;
  }
}