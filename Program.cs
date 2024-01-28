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
            // Definizione delle sezioni del Google Play Store da cui raccogliere i dati.
            string[] sections = { "phone", "car", "watch", "chromebook", "tv", "tablet" };
            List<string> allAppUrls = new List<string>();

            // Avvio della misurazione del tempo totale di esecuzione.
            TimeMeasurement totalTimeMeasurement = new TimeMeasurement();
            totalTimeMeasurement.Start();

            // Utilizzo di HttpClient per effettuare le richieste HTTP.
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

                        // Variabile per tenere traccia del numero totale di app raccolte per questa sezione
                        int totalAppsCollectedInSection = 0;

                        // Ciclo per la raccolta degli URL delle app.
                        do
                        {
                            HttpResponseMessage sectionResponse = await httpClient.GetAsync(sectionUrl);

                            if (sectionResponse.IsSuccessStatusCode)
                            {
                                // Leggi il contenuto della pagina e ottieni gli URL delle pagine di applicazioni
                                string sectionContent = await sectionResponse.Content.ReadAsStringAsync();
                                HtmlDocument sectionDoc = new HtmlDocument();
                                sectionDoc.LoadHtml(sectionContent);

                                // Estrazione degli URL delle app dalla pagina HTML.
                                var sectionAppLinks = sectionDoc.DocumentNode.SelectNodes("//a[starts-with(@href, '/store/apps/details')]");

                                if (sectionAppLinks != null)
                                {
                                    foreach (var linkNode in sectionAppLinks)
                                    {
                                        string appUrl = "https://play.google.com" + linkNode.GetAttributeValue("href", string.Empty);
                                        allAppUrls.Add(appUrl);
                                        totalAppsCollectedInSection++; // Incrementa il conteggio delle app per questa sezione
                                    }
                                }

                                // Verifica se ci sono altre pagine di app disponibili
                                var nextPageLink = sectionDoc.DocumentNode.SelectSingleNode("//a[@class='CwaK9']");
                                if (nextPageLink != null)
                                {
                                    sectionUrl = "https://play.google.com" + nextPageLink.GetAttributeValue("href", string.Empty);
                                }
                                else
                                {
                                    // Non ci sono altre pagine disponibili, esci dal ciclo
                                    break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Errore nella richiesta: {sectionResponse.StatusCode}");
                                break;
                            }

                        } while (totalAppsCollectedInSection < 1000); // limite di 1000 app

                        Console.WriteLine($"Raccolti {totalAppsCollectedInSection} URL di app per la sezione {section}.");
                    }

                    // Interrompi la misurazione del tempo
                    timeMeasurement.Stop();

                    // Ottieni il tempo trascorso in millisecondi
                    long elapsedTime = timeMeasurement.GetElapsedTimeMilliseconds();

                    Console.WriteLine($"Tempo impiegato per la richiesta HTTP: {elapsedTime} ms");

                    // Estrazione metadati dagli URL delle app
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

        // Metodo per il processamento asincrono di ogni app e l'estrazione dei metadati.
        private static async Task ProcessAppAsync(string appUrl, string appId)
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage appResponse = await httpClient.GetAsync(appUrl);

                    if (appResponse.IsSuccessStatusCode)
                    {
                        BufferManager bufferManager = new BufferManager(2000000);

                        using (var stream = await appResponse.Content.ReadAsStreamAsync())
                        {
                            await bufferManager.ReadStreamAsync(stream);
                        }

                        string appHtmlContent = bufferManager.GetBufferedContent();

                        HtmlDocument appHtmlDoc = new HtmlDocument();
                        appHtmlDoc.LoadHtml(appHtmlContent);

                        // Estrazione dei metadati dall'HTML della pagina dell'app.
                        var titleElement = appHtmlDoc.DocumentNode.SelectSingleNode("//h1[@itemprop='name']");
                        var categoryElement = appHtmlDoc.DocumentNode.SelectSingleNode("//div[@itemprop='genre']/span");
                        var starsElement = appHtmlDoc.DocumentNode.SelectSingleNode("(//div[@itemprop='starRating']/div)[1]");
                        string starsValue = null;
                        if (starsElement != null)
                        {
                            string ratingText = starsElement.InnerText;
                            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(ratingText, @"\d+\.\d+");

                            if (match.Success)
                            {
                                double ratingValue = double.Parse(match.Value);
                                starsValue = ratingValue.ToString();
                            }
                        }
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
