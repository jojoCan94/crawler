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
            string url = "https://play.google.com/store/apps";

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

                    // Effettuare la richiesta GET
                    HttpResponseMessage response = await httpClient.GetAsync(url);

                    // Interrompi la misurazione del tempo
                    timeMeasurement.Stop();

                    // Ottieni il tempo trascorso in millisecondi
                    long elapsedTime = timeMeasurement.GetElapsedTimeMilliseconds();

                    Console.WriteLine($"Tempo impiegato per la richiesta HTTP: {elapsedTime} ms");

                    // Verificare se la richiesta è andata a buon fine
                    if (response.IsSuccessStatusCode)
                    {
                        BufferManager bufferManager = new BufferManager(2000000);

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            // Leggi il flusso e accumula i dati utilizzando BufferManager
                            await bufferManager.ReadStreamAsync(stream);
                        }

                        // ottieni il contenuto della risposta HTTP
                        string content = bufferManager.GetBufferedContent();

                        htmlDoc.LoadHtml(content);

                        // Utilizza XPath per trovare tutti i link delle app nel Play Store
                        var appLinks = htmlDoc.DocumentNode.SelectNodes("//a[starts-with(@href, '/store/apps/details')]");

                        if (appLinks != null)
                        {
                            int appsToRetrieve = 1000;
                            int appsRetrieved = 0;
                            List<JObject> batchData = new List<JObject>();

                            foreach (var linkNode in appLinks)
                            {
                                // Estrai l'URL dell'app
                                string appUrl = "https://play.google.com" + linkNode.GetAttributeValue("href", string.Empty);

                                // estrai l'ID dell'app dall'URL
                                string appId = appUrl.Split('=').Last();

                                // Estrai i metadati dell'app
                                JObject appMetadata = await GetAppMetadataAsync(appUrl);
                                batchData.Add(appMetadata);

                                appsRetrieved++;

                                if(appsRetrieved >= appsToRetrieve)
                                {
                                    // Salva i metadati in un file JSON dentro una cartella chiamata "apps"
                                    string directory = "apps";
                                    Directory.CreateDirectory(directory);

                                    string fileName = $"{directory}/{appId}.json";
                                    File.WriteAllText(fileName, appMetadata.ToString());

                                    batchData.Clear();
                                }

                                if (appsRetrieved >= appsToRetrieve)
                                {
                                    break;
                                }
                            }

                            Console.WriteLine($"Raccolti {appsRetrieved} metadati di app.");
                        }

                        totalTimeMeasurement.Stop();

                        // Ottieni il tempo totale trascorso
                        long totalElapsedTime = totalTimeMeasurement.GetElapsedTimeMilliseconds();
                        Console.WriteLine($"Tempo totale impiegato: {totalElapsedTime} ms");
                    }
                    else
                    {
                        Console.WriteLine($"Errore nella richiesta: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore durante la richiesta: {ex.Message}", ex);
                }
            }
        }

        // Estrae i metadati di un'app dal suo URL
        private static async Task<JObject> GetAppMetadataAsync(string appUrl)
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
                        BufferManager bufferManager = new BufferManager(1000000);
        
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

                        return appData;
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
        
            // In caso di errore o se non riesci a ottenere i metadati, restituisci un JObject vuoto o null
            return new JObject();
        }


    }
}
