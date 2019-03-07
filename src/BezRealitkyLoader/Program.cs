using AngleSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BezRealitkyLoader
{
    class Program
    {
        static IConfiguration AngleSharpConfiguration = Configuration.Default.WithDefaultLoader();
        const string DomainName = "https://www.bezrealitky.cz";
        const string ListingUrlTemplate = "/vypis/nabidka-{0}/byt/praha/praha-{1}?order=time_order_desc"; // Load all
        //const string ListingUrlTemplate = "/vypis/nabidka-pronajem/byt/praha/praha-{0}?disposition%5B0%5D=garsoniera&disposition%5B1%5D=1-kk&disposition%5B2%5D=1-1&order=time_order_desc"; // Load only small apts

        static void Main(string[] args)
        {
            string listingType = "prodej"; //or pronajem
            string outputDirectory = ".\\"; // current working directory
            string[] districts = new string[] { "vinohrady", "smichov", "kosire", "strasnice", "nusle", "zizkov", "dejvice", "michle", "malesice", "karlin",
                "podoli", "vysehrad", "vysocany", "holesovice", "hloubetin", "jinonice", "kobylisy", "krc", "radlice", "stresovice", "vrsovice", "zabehlice" };

            LoadApartments(outputDirectory, listingType, districts).Wait();
        }

        private static async Task LoadApartments(string outputDirectory, string listingType, string[] districts)
        {
            var apartmentsByDistrict = new Dictionary<string, List<Apartment>>();

            foreach (string district in districts)
            {
                List<Apartment> districtApartments = await LoadDistrict(listingType, district);

                apartmentsByDistrict.Add(district, districtApartments);
            }

            SaveResults(outputDirectory, listingType, apartmentsByDistrict);
        }

        private static void SaveResults(string outputDirectory, string listingType, Dictionary<string, List<Apartment>> apartmentsByDistrict)
        {
            string outputType = listingType == "pronajem" ? "Rentals" : "Sells";

            // Generate flat structure
            List<Apartment> allApartments = new List<Apartment>();
            foreach (var district in apartmentsByDistrict)
            {
                foreach (var apartment in district.Value)
                {
                    apartment.District = district.Key;

                    allApartments.Add(apartment);
                }
            }

            Console.WriteLine();

            // one-time results
            string rentalsByDistrictJson = JsonConvert.SerializeObject(apartmentsByDistrict, Formatting.Indented);
            string rentalsByDistrictFile = Path.Combine(outputDirectory, outputType + "ByDistrict.json");
            File.WriteAllText(rentalsByDistrictFile, rentalsByDistrictJson);
            Console.WriteLine("{0} by district stored to {1}", outputType, rentalsByDistrictFile);

            string rentalsFlatJson = JsonConvert.SerializeObject(allApartments, Formatting.Indented);
            string rentalsFlatFile = Path.Combine(outputDirectory, outputType + ".json");
            File.WriteAllText(rentalsFlatFile, rentalsFlatJson);
            Console.WriteLine("All {0} stored to {1}", outputType, rentalsFlatFile);

            string rentalsCsv = ServiceStack.Text.CsvSerializer.SerializeToCsv(allApartments);
            string rentalsFile = Path.Combine(outputDirectory, outputType + ".csv");
            File.WriteAllText(rentalsFile, rentalsCsv);
            Console.WriteLine("All {0} stored to {1}", outputType, rentalsFlatFile);

            // all-time database
            string allRentalsFile = Path.Combine(outputDirectory, "All" + outputType + ".json");
            List<Apartment> existingApartments = new List<Apartment>();
            if (File.Exists(allRentalsFile))
            {
                existingApartments = JsonConvert.DeserializeObject<List<Apartment>>(File.ReadAllText(allRentalsFile));
            }

            int newOccurences = 0;
            foreach (var apartment in allApartments)
            {
                if (existingApartments.Contains(apartment))
                {
                    var existingApartment = existingApartments.Where(a => a.Id == apartment.Id).FirstOrDefault();
                    existingApartment.LastSeen = DateTime.Now;
                    existingApartment.Status = apartment.Status;
                }
                else
                {
                    apartment.FirstSeen = DateTime.Now;
                    apartment.LastSeen = DateTime.Now;
                    existingApartments.Add(apartment);

                    newOccurences++;
                }
            }

            string updatedJsonData = JsonConvert.SerializeObject(existingApartments, Formatting.Indented);
            File.WriteAllText(allRentalsFile, updatedJsonData);

            Console.WriteLine("All {2} database updated at {0} file with {1} newly added listings.", allRentalsFile, newOccurences, outputType);
        }

        private static async Task<List<Apartment>> LoadDistrict(string listingType, string district)
        {
            Console.Write("Loading apartments from {0}...", district);

            string path = string.Format(ListingUrlTemplate, listingType, district);
            string url = string.Format("{0}{1}", DomainName, path);
            var document = await BrowsingContext.New(AngleSharpConfiguration).OpenAsync(url);

            List<string> additionalPages = new List<string>();

            var paging = document.QuerySelectorAll("ul.pagination a.page-link");
            if (paging != null && paging.Length > 0)
            {
                var lastPage = paging[paging.Length - 2];
                int lastPageNumber = int.Parse(lastPage.TextContent);
                string urlTemplate = Regex.Replace(lastPage.GetAttribute("href"), "=" + lastPage.TextContent + "$", "={0}");
                if (lastPageNumber > 1)
                {
                    for (int i = 2; i <= lastPageNumber; i++)
                    {
                        string pageUrl = string.Format(urlTemplate, i);
                        additionalPages.Add(pageUrl);
                    }
                }
            }

            // from main page
            List<Apartment> apartments = ParseApartments(document, district);

            // and then additional pages
            foreach (string nextUrl in additionalPages)
            {
                var pageDocument = await BrowsingContext.New(AngleSharpConfiguration).OpenAsync(string.Format("{0}{1}", DomainName, nextUrl));

                var pageApartments = ParseApartments(pageDocument, district);

                apartments.AddRange(pageApartments);
            }

            Console.WriteLine(" {0} apartments found.", apartments.Count);

            return apartments;
        }

        private static List<Apartment> ParseApartments(AngleSharp.Dom.IDocument document, string disctrict)
        {
            List<Apartment> apartments = new List<Apartment>();

            var ads = document.QuerySelectorAll("article.product");
            foreach (var ad in ads)
            {
                var apartment = new Apartment
                {
                    Status = Status.Available,
                    District = disctrict
                };

                string idAttribute = ad.GetAttribute("id");
                int id = int.Parse(idAttribute.Substring(idAttribute.IndexOf("-") + 1));
                apartment.Id = id;

                var detailElement = ad.QuerySelector("h3 strong");
                string street = detailElement.TextContent.Trim();
                apartment.Address = street;

                string[] metadata = null;
                var element = ad.QuerySelector("p.product__note");
                if (element.TextContent.Contains("Pronájem bytu"))
                {
                    var priceElement = ad.QuerySelector("strong.product__value");
                    string price = priceElement.TextContent;
                    if (price.Contains("+"))
                    {
                        string[] prices = price.Split('+');
                        apartment.Rent = decimal.Parse(prices[0].Replace("Kč", "").Replace(".", "").Trim());
                        apartment.Fees = decimal.Parse(prices[1].Replace("Kč", "").Replace(".", "").Trim());
                    }

                    metadata = element.TextContent.Replace("Pronájem bytu ", "").Split(',');
                }
                if (element.TextContent.Contains("Prodej bytu"))
                {
                    var priceElement = ad.QuerySelector("strong.product__value");
                    string price = priceElement.TextContent;
                    if (price.Contains("+"))
                    {
                        string[] prices = price.Split('+');
                        price = prices[0];
                    }
                    apartment.PurchasePrice = decimal.Parse(price.Replace("Kč", "").Replace(".", "").Trim());

                    metadata = element.TextContent.Replace("Prodej bytu ", "").Split(',');
                }

                if (metadata.Length == 2)
                {
                    apartment.Disposition = metadata[0].Trim();
                    apartment.Area = int.Parse(metadata[1].Replace(" m²", "").Trim());
                }

                element = ad.QuerySelector("p.product__info-text");
                apartment.Description = element.TextContent.Trim();

                element = ad.QuerySelector("a.product__link");
                apartment.DetailsLink = element.GetAttribute("href");

                // additional attributes
                element = ad.QuerySelector("div.product__header span.product__label span.badge");
                if (element != null)
                {
                    switch (element.TextContent)
                    {
                        case "Nabídka Premium uživatele":
                            apartment.IsPremiumOffer = true;
                            break;
                        case "Rezervováno":
                            apartment.Status = Status.Reserved;
                            break;
                    }
                }

                apartments.Add(apartment);
            }

            return apartments;
        }
    }
}
