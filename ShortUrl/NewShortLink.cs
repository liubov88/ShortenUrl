namespace ShortUrl;
public class NewShortLink
{
  public int Id { get; set; }
  public string Link { get; set; }
  public string GetNewUrl()
  {
    return WebEncoders.Base64UrlEncode(BitConverter.GetBytes(Id));
  }
  public static int GetById(string url)
  {
    return BitConverter.ToInt32(WebEncoders.Base64UrlDecode(url));
  }
}
