using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WorkerServiceCopyFileByAPI
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        static HttpClient client = new HttpClient();
        private string pathFrom, pathTo;
        private IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            pathFrom = _configuration["FilePath:PathFrom"];
            pathTo = _configuration["FilePath:PathTo"];
            _logger.LogInformation("==== Service started at: {time}", DateTimeOffset.Now);
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                client.BaseAddress = new Uri("https://localhost:7127/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/Login", new { Username = "anhdd", Password = "123456" });
                response.EnsureSuccessStatusCode();

                ObjToken token = await response.Content.ReadAsAsync<ObjToken>();

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                    client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token.api_token);
                    HttpResponseMessage sayRes = await client.PostAsJsonAsync(
            "api/WeatherForecast/Say", DateTime.Now.ToString());
                    sayRes.EnsureSuccessStatusCode();

                    string res = await sayRes.Content.ReadAsAsync<string>();

                    CopyFilesAsync(pathFrom, pathTo, res);

                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            
        }

        async Task<string> CopyFilesAsync(string pathFrom, string pathTo, string text)
        {
            try
            {
                if (Directory.Exists(pathFrom))
                {
                    FileInfo[] fileOfFrom = new DirectoryInfo(pathFrom).GetFiles("*.txt");

                    if (!Directory.Exists(pathTo))
                    {
                        Directory.CreateDirectory(pathTo);
                        _logger.LogInformation("Đã tạo thư mục " + pathTo);
                    }

                    FileInfo[] fileOfTo = new DirectoryInfo(pathTo).GetFiles();

                    int bufferSize = 1024 * 1024;
                    foreach (FileInfo file in fileOfFrom)
                    {
                        FileInfo? fileInfo = Array.Find(fileOfTo, element => element.Name.Equals(file.Name));
                        if (fileInfo == null || file.LastWriteTime > fileInfo.LastWriteTime)
                        {
                            try
                            {
                                string fileTo = pathTo + "\\" + file.Name;
                                _logger.LogInformation("Bắt đầu ghi file: " + file.Name);
                                using (FileStream fileStream = new FileStream(fileTo, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                                {
                                    FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite);
                                    fileStream.SetLength(fs.Length);
                                    int bytesRead = -1;
                                    byte[] bytes = new byte[bufferSize];

                                    while ((bytesRead = fs.Read(bytes, 0, bufferSize)) > 0)
                                    {
                                        fileStream.Write(bytes, 0, bytesRead);
                                    }
                                }

                                using (StreamWriter sw = File.AppendText(fileTo))
                                {
                                    sw.WriteLine("\n" + text);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Lỗi khi ghi file: " + ex);
                            }

                        }
                    }
                }
                else
                {
                    _logger.LogError("Thư mục nguồn không tồn tại: " + pathFrom);
                    Console.WriteLine("Thư mục nguồn không tồn tại: " + pathFrom);
                }
                return "Copy thành công";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Có lỗi trong quá trình xử lý! {ex}");
                return $"Có lỗi trong quá trình xử lý! {ex}";
            }
        }

    }

    public class ObjToken
    {
        public string api_token { get; set; }
        public DateTime expiration { get; set; }
    }
}