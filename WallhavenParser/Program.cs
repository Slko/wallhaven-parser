using HtmlAgilityPack;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WallhavenParser
{
    class Program
    {
        private HttpClient _http = new HttpClient();
        private Random _random = new Random();
        private const string Query = "geometry";
        private TimeSpan Delay = TimeSpan.FromMinutes(30);
        private async Task<HtmlNode> ParseDocumentFromURL(string url) => await new Func<HtmlDocument, Task<HtmlNode>>(async (d) => { d.LoadHtml(await _http.GetStringAsync(url)); return d.DocumentNode; })(new HtmlDocument());
        private int TryParseInt(string s) => s == null ? 0 : int.Parse(s);
        private async Task<HtmlNodeCollection> XPath(string url, string xpath) => (await ParseDocumentFromURL(url)).SelectNodes(xpath);
        private async Task<HtmlNode> XPathOne(string url, string xpath) => (await ParseDocumentFromURL(url)).SelectSingleNode(xpath);
        private string GetURL(string query, int page = 1) => $"http://alpha.wallhaven.cc/search?q={HttpUtility.UrlEncode(query)}&page={page}";
        private async Task<int> GetPageCount(string query) => TryParseInt((await XPathOne(GetURL(query), "//section[@class='thumb-listing-page']/header"))?.InnerText?.Split(' ')?.LastOrDefault());
        private async Task<int[]> GetImages(string query, int page) => (await XPath(GetURL(query, page), "//a[@class='preview']")).Select(a => int.Parse(a.Attributes["href"].Value.Split('/').Last())).ToArray();
        private string FixScheme(string url) => new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps }.Where(s => s == new Uri(url).Scheme).Count() == 0 ? "http:" + url : url;
        private async Task<string> GetImage(int id) => FixScheme((await XPathOne($"http://alpha.wallhaven.cc/wallpaper/{id}", "//img[@id='wallpaper']")).Attributes["src"].Value);
        private async Task While(Func<Task<bool>> callback, Func<Task> after) => await new Func<Task>(async () => { while (await callback()) { await after(); } })();
        private async Task<int?> GetRandomPage(string query) => new Func<int, int?>(c => c == 0 ? (int?)null : _random.Next(1, c + 1))(await GetPageCount(query));
        private async Task<int[]> GetRandomImages(string query) => await new Func<int?, Task<int[]>>(async (p) => p == null ? null : await GetImages(query, p.Value))(await GetRandomPage(query));
        private async Task Using<T>(T obj, Func<T, Task> callback) => await new Func<Task>(async () => { await callback(obj); (obj as IDisposable)?.Dispose(); })();
        private async Task DownloadFile(string url, string dest) => await Using(await _http.GetStreamAsync(url), async (download) => await Using(File.Open(dest, FileMode.Create), async (file) => await download.CopyToAsync(file)));
        private T PickRandom<T>(T[] arr) => arr[_random.Next(arr.Length)];
        private async Task DownloadImage(int id) => await DownloadFile(await GetImage(id), $"C:\\Wallpapers\\{id}.jpg");
        private async Task<T> WaitAndReturn<T>(Task task, T value) => await new Func<Task<T>>(async () => { await task; return value; })();
        private async Task<bool> Do(string query) => await new Func<int[], Task<bool>>(async (imgs) => imgs == null ? false : (imgs.Length == 0 ? true : await WaitAndReturn(DownloadImage(PickRandom(imgs)), true)))(await GetRandomImages(query));
        private bool PrintIfFalse(bool value, string msg) => new Func<bool>(() => { if(!value) Console.WriteLine(msg); return value; })();
        private async Task MainAsync() => await While(async () => PrintIfFalse(await Do(Query), "Nobody here but us chickens!"), async () => await Task.Delay(Delay));
        private static void Main(string[] args) => AsyncContext.Run(() => new Program().MainAsync());
    }
}
