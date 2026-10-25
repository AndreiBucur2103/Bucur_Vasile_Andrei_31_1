using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using WordFrequency;

namespace ExtragereTrasaturi
{
    class ProcessedDocument
    {
        public string SourceFolder { get; set; }
        public string FileName { get; set; }
        public List<string> Topics { get; set; }
        public List<string> CleanedTokens { get; set; }
    }

    class Program
    {
        private static HashSet<string> stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static PorterStemmer porterStemmer = new PorterStemmer();

        static void Main(string[] args)
        {
            string rootPath = @"C:\Users\Andrei\Desktop\SistemeRecunoastereLab\Extragere";
            string stopwordsPath = Path.Combine(rootPath, "stopwords.txt");
            string outputPath = Path.Combine(rootPath, "output_vectors.txt");

            LoadStopwords(stopwordsPath);

            List<ProcessedDocument> processedDocuments = new List<ProcessedDocument>();
            string[] folders = new string[] { "Training", "Testing" };

            Console.WriteLine("Incepe citirea si preprocesarea fisierelor XML cu Porter Stemmer...\n");

            foreach (string folderName in folders)
            {
                string folderPath = Path.Combine(rootPath, folderName);
                if (!Directory.Exists(folderPath)) continue;

                string[] xmlFiles = Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories);

                foreach (string filePath in xmlFiles)
                {
                    ProcessedDocument doc = ParseAndPreprocessXml(filePath, folderName);
                    if (doc != null)
                    {
                        processedDocuments.Add(doc);
                    }
                }
            }

            Console.WriteLine($"Am preprocesat cu succes {processedDocuments.Count} documente.");

            Console.WriteLine("Se genereaza dictionarul global...");
            List<string> globalDictionary = processedDocuments
                .SelectMany(d => d.CleanedTokens)
                .Distinct()
                .OrderBy(w => w)
                .ToList();

            Console.WriteLine($"Dimensiunea Dictionarului Global: {globalDictionary.Count} cuvinte unice.");

            Dictionary<string, int> wordToGlobalIndex = globalDictionary
                .Select((word, index) => new { word, index })
                .ToDictionary(x => x.word, x => x.index);

            Console.WriteLine($"Se scrie rezultatul în: {outputPath} ...");

            using (StreamWriter writer = new StreamWriter(outputPath))
            {
                foreach (string word in globalDictionary)
                {
                    writer.WriteLine($"@attribute {word}");
                }

                writer.WriteLine("@data");

                int docCounter = 1;
                foreach (var doc in processedDocuments)
                {
                    var localCounts = new Dictionary<string, int>();
                    foreach (var token in doc.CleanedTokens)
                    {
                        if (localCounts.ContainsKey(token))
                            localCounts[token]++;
                        else
                            localCounts[token] = 1;
                    }

                    var sparseVector = localCounts
                        .Where(pair => wordToGlobalIndex.ContainsKey(pair.Key))
                        .Select(pair => new { Index = wordToGlobalIndex[pair.Key], Count = pair.Value })
                        .OrderBy(x => x.Index)
                        .Select(x => $"{x.Index}:{x.Count}");

                    string sparseVectorStr = string.Join(" ", sparseVector);
                    string topicsStr = string.Join(" ", doc.Topics);

                    writer.WriteLine($"D{docCounter++} # {sparseVectorStr} # {topicsStr} # {doc.SourceFolder}");
                }
            }

            Console.WriteLine("\n--- PROCESARE SI GENERARE FINALIZATĂ CU SUCCES! ---");
            Console.WriteLine($"Fisierul 'output_vectors.txt' a fost salvat.");
            Console.ReadKey();
        }

        private static void LoadStopwords(string filePath)
        {
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    string word = line.Trim().ToLower();
                    if (!string.IsNullOrEmpty(word))
                    {
                        stopwords.Add(word);
                    }
                }
            }
        }

        private static ProcessedDocument ParseAndPreprocessXml(string filePath, string folderName)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);

                XmlNode titleNode = xmlDoc.SelectSingleNode("//title");
                string title = titleNode != null ? titleNode.InnerText : "";

                XmlNodeList pNodes = xmlDoc.SelectNodes("//text/p");
                List<string> paragraphs = new List<string>();
                if (pNodes != null)
                {
                    foreach (XmlNode pNode in pNodes)
                    {
                        paragraphs.Add(pNode.InnerText);
                    }
                }

                string fullRawText = title + " " + string.Join(" ", paragraphs);

                List<string> topics = new List<string>();
                XmlNode topicsCodesNode = xmlDoc.SelectSingleNode("//codes[@class='bip:topics:1.0']");
                if (topicsCodesNode != null)
                {
                    XmlNodeList codeNodes = topicsCodesNode.SelectNodes("code");
                    if (codeNodes != null)
                    {
                        foreach (XmlNode codeNode in codeNodes)
                        {
                            if (codeNode.Attributes?["code"] != null)
                            {
                                topics.Add(codeNode.Attributes["code"].Value);
                            }
                        }
                    }
                }

                List<string> cleanedTokens = PreprocessText(fullRawText);

                return new ProcessedDocument
                {
                    SourceFolder = folderName,
                    FileName = Path.GetFileName(filePath),
                    Topics = topics,
                    CleanedTokens = cleanedTokens
                };
            }
            catch
            {
                return null;
            }
        }

        private static List<string> PreprocessText(string rawText)
        {
            string text = rawText.ToLower();
            text = Regex.Replace(text, @"[^a-z\s]", " ");

            string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            List<string> result = new List<string>();
            foreach (string token in tokens)
            {
                if (stopwords.Contains(token)) continue;

                string stemmed = porterStemmer.StemWord(token);

                if (stemmed.Length > 1)
                {
                    result.Add(stemmed);
                }
            }

            return result;
        }
    }
}