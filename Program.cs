using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;

namespace GooglePlayStoreCrawler
{
    class Program
    {
        // Creare un oggetto HtmlDocument e caricare l'HTML
        static HtmlDocument htmlDoc = new HtmlDocument();
        static async Task Main()
        {
            // URL della pagina web da cui raccogliere i dati
            string[] sections = { "phone", "car", "watch", "chromebook", "tv", "tablet" };
            List<string> allAppUrls = new List<string>();

            // Inizia a misurare il tempo
            TimeMeasurement totalTimeMeasurement = new TimeMeasurement();
            totalTimeMeasurement.Start();

            // Creare un'istanza di HttpClient per effettuare la richiesta HTTP
            using (HttpClient httpClient = new HttpClient())
            {
                TimeMeasurement timeMeasurement = new TimeMeasurement();

                try
                {
                    // Start timer
                    timeMeasurement.Start();

                    foreach (var section in sections)
                    {
                        string sectionUrl = $"https://play.google.com/store/apps?device={section}";

                        HttpResponseMessage sectionResponse = await httpClient.GetAsync(sectionUrl);

                        if (sectionResponse.IsSuccessStatusCode)
                        {
                            // Leggi il contenuto della pagina e ottieni gli URL delle pagine di applicazioni
                            string sectionContent = await sectionResponse.Content.ReadAsStringAsync();
                            HtmlDocument sectionDoc = new HtmlDocument();
                            sectionDoc.LoadHtml(sectionContent);
                            var sectionAppLinks = sectionDoc.DocumentNode.SelectNodes("//a[starts-with(@href, '/store/apps/details')]");

                            if (sectionAppLinks != null)
                            {
                                Console.WriteLine($"Raccolti {sectionAppLinks.Count} URL di app per la sezione {section}.");
                                foreach (var linkNode in sectionAppLinks)
                                {
                                    string appUrl = "https://play.google.com" + linkNode.GetAttributeValue("href", string.Empty);
                                    allAppUrls.Add(appUrl);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Errore nella richiesta: {sectionResponse.StatusCode}");
                        }

                        // raccogli i dati di 200 app per sezione usando la lista allAppUrls
                        if (allAppUrls.Count >= 1000)
                        {
                            break;
                        }
                    }

                    // Interrompi la misurazione del tempo
                    timeMeasurement.Stop();

                    // Ottieni il tempo trascorso in millisecondi
                    long elapsedTime = timeMeasurement.GetElapsedTimeMilliseconds();

                    Console.WriteLine($"Tempo impiegato per la richiesta HTTP: {elapsedTime} ms");

                    // Procedi con il recupero dei dati delle applicazioni
                    List<Task> tasks = new List<Task>();

                    foreach (var appUrl in allAppUrls)
                    {
                        string appId = appUrl.Split('=').Last();
                        tasks.Add(ProcessAppAsync(appUrl, appId));
                    }

                    await Task.WhenAll(tasks);

                    Console.WriteLine($"Raccolti {allAppUrls.Count} metadati di app.");

                    totalTimeMeasurement.Stop();

                    // Ottieni il tempo totale trascorso
                    long totalElapsedTime = totalTimeMeasurement.GetElapsedTimeMilliseconds();
                    Console.WriteLine($"Tempo totale impiegato: {totalElapsedTime} ms");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore durante la richiesta: {ex.Message}", ex);
                }
            }
        }

        private static async Task ProcessAppAsync(string appUrl, string appId)
        {
            try
            {
                // Creare un'istanza di HttpClient per effettuare la richiesta HTTP all'URL dell'app
                using (HttpClient httpClient = new HttpClient())
                {
                    // Effettuare la richiesta GET all'URL dell'app
                    HttpResponseMessage appResponse = await httpClient.GetAsync(appUrl);

                    // Verificare se la richiesta è andata a buon fine
                    if (appResponse.IsSuccessStatusCode)
                    {
                        BufferManager bufferManager = new BufferManager(2000000);

                        using (var stream = await appResponse.Content.ReadAsStreamAsync())
                        {
                            // Leggi il flusso e accumula i dati utilizzando BufferManager
                            await bufferManager.ReadStreamAsync(stream);
                        }

                        // Ottieni il contenuto della risposta HTTP
                        string appHtmlContent = bufferManager.GetBufferedContent();

                        // Crea un oggetto HtmlDocument per analizzare la pagina dell'app
                        HtmlDocument appHtmlDoc = new HtmlDocument();
                        appHtmlDoc.LoadHtml(appHtmlContent);

                        // Utilizza XPath per trovare gli elementi <h1> con itemprop="name"
                        var titleElement = appHtmlDoc.DocumentNode.SelectSingleNode("//h1[@itemprop='name']");

                        var categoryElement = appHtmlDoc.DocumentNode.SelectSingleNode("//div[@itemprop='genre']/span");

                        var starsElement = appHtmlDoc.DocumentNode.SelectSingleNode("(//div[@itemprop='starRating']/div)[1]");
                        string starsValue = null;
                        if (starsElement != null)
                        {
                            string ratingText = starsElement.InnerText;

                            // Utilizza un'espressione regolare per estrarre il valore numerico
                            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(ratingText, @"\d+\.\d+");

                            if (match.Success)
                            {
                                double ratingValue = double.Parse(match.Value);
                                starsValue = ratingValue.ToString();
                            }
                        }

                        // Utilizza XPath per trovare l'elemento <span> con itemprop="contentRating"
                        var contentRatingElement = appHtmlDoc.DocumentNode.SelectSingleNode("//span[@itemprop='contentRating']");

                        JObject appData = new JObject
                        {
                            { "Title", titleElement.InnerText },
                            { "Category", categoryElement.InnerText },
                            { "Stars", starsValue },
                            { "ContentRating", contentRatingElement.InnerText }
                        };

                        // Salva i metadati in un file JSON dentro una cartella chiamata "apps"
                        string directory = "apps";
                        Directory.CreateDirectory(directory);

                        string fileName = $"{directory}/{appId}.json";
                        File.WriteAllText(fileName, appData.ToString());
                    }
                    else
                    {
                        Console.WriteLine($"Errore nella richiesta all'URL dell'app: {appResponse.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'estrazione dei metadati dell'app: {ex.Message}");
            }
        }
    }
}
