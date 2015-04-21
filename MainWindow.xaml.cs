using System.Diagnostics;

namespace Findkaninen
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using Findkaninen.Containers;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Anagram> anagrammer = new List<Anagram>();
        private int noOfCharsInAnagram;
        private List<char> allowedchars;
        private Dictionary<string, List<string>> alfabetiseretListe;
        private int totalFoundAnagrams;
        private DateTime startTime;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private static List<char> CharsOk(string str, List<char> okchars)
        {
            return CharsOk(str.ToCharArray(), okchars);
        }

        private static List<char> CharsOk(char[] charsToCheck, List<char> okchars)
        {
            var okcharstmp = okchars.ToList();

            if (charsToCheck.Length == 0)
            {
                return null;
            }

            foreach (var c in charsToCheck)
            {
                if (c == '\'')
                {
                    continue;
                }

                if (okcharstmp.Contains(c))
                {
                    okcharstmp.Remove(c);
                }
                else
                {
                    return null;
                }
            }

            return okcharstmp;
        }

        private static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }

            return sb.ToString().ToLower();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var phrase = subjectphrase.Text;
            await Task.Run(() => { Start(phrase); });
        }

        private void Start(string phrase)
        {
            string[] dict = this.InitFindAnagrams(phrase);

            var sw = new Stopwatch();
            sw.Start();
            var oklistwords = dict.Select(w => Regex.Replace(w, @"\s", string.Empty)).GroupBy(w => w).Where(g => CharsOk(g.Key, this.allowedchars) != null).Select(g => g.Key);
            sw.Stop();
            Debug.WriteLine("cleandict: " + sw.ElapsedMilliseconds);

            sw.Restart();
            //lav alfabetiseret liste
            this.alfabetiseretListe = this.LavAlfabetiseretOrdbog(oklistwords);
            var keys = this.alfabetiseretListe.Keys.ToArray();
            sw.Stop();
            Debug.WriteLine("Time to compute alfabetized dictionary: " + sw.ElapsedMilliseconds);

            sw.Restart();
            this.GetAnagrams(keys, 3, new List<string>(), this.allowedchars);
            sw.Stop();
            Debug.WriteLine("Time to compute anagrams: " + sw.ElapsedMilliseconds);

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { anagramgrid.ItemsSource = anagrammer.OrderByDescending(a => a.Text); }));
        }

        private string[] InitFindAnagrams(string phrase)
        {
            this.startTime = DateTime.Now;
            this.totalFoundAnagrams = 0;
            this.anagrammer.Clear();
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { anagramgrid.ItemsSource = null; }));
            this.allowedchars = phrase.Replace(" ", string.Empty).ToCharArray().ToList();
            this.noOfCharsInAnagram = this.allowedchars.Count;
            return File.ReadAllLines("wordlist.txt");
        }

        private Dictionary<string, List<string>> LavAlfabetiseretOrdbog(IEnumerable<string> oklistwords)
        {
            var alfabetiseretListe = new Dictionary<string, List<string>>();
            foreach (var ord in oklistwords)
            {
                var alfebetiseretOrd = new string(ord.ToCharArray().Where(c => c != '\'').OrderBy(c => c).ToArray());
                if (alfabetiseretListe.ContainsKey(alfebetiseretOrd))
                {
                    alfabetiseretListe[alfebetiseretOrd].Add(ord);
                }
                else
                {
                    alfabetiseretListe.Add(alfebetiseretOrd, new List<string> { ord });
                }
            }

            return alfabetiseretListe;
        }

        private IEnumerable<IEnumerable<string>> ConvertAlfabetizedAnagramToRealAnagrams(IEnumerable<string> anagramIN)
        {
            var anagram = new Stack<string>(anagramIN);

            if (anagram.Count > 0)
            {
                var word = anagram.Pop();
                var alphabetizisedEquivalents = this.alfabetiseretListe[word];
                if (anagram.Count == 0)
                {
                    foreach (var w in alphabetizisedEquivalents)
                    {
                        yield return new[] { w };
                    }
                }
                else
                {
                    foreach (var subAnagrams in this.ConvertAlfabetizedAnagramToRealAnagrams(anagram))
                    {
                        foreach (var w in alphabetizisedEquivalents)
                        {
                            yield return subAnagrams.Concat(new[] { w });
                        }
                    }
                }
            }
        }

        private IEnumerable<List<string>> Permute(List<string> anagram)
        {
            if (anagram.Count == 1)
            {
                yield return new List<string>(anagram);
            }
            else
            {
                foreach (var word in anagram)
                {
                    var subanagram = new List<string>(anagram);
                    subanagram.Remove(word);
                    foreach (var subperm in this.Permute(subanagram))
                    {
                        subperm.Add(word);
                        yield return subperm;
                    }
                }
            }
        }

        private void GetAnagrams(string[] dictKeys, int worddepth, List<string> ordDerSkalVæreMedIAnagram, List<char> tmpOkChars, int ordDerSkalVæreMedLength = 0, int dictStartPos = 0)
        {
            for (var i = dictStartPos; i < dictKeys.Length; i++)
            {
                var ord = dictKeys[i];
                var anagramLength = ordDerSkalVæreMedLength + ord.Length;

                //tjek om det er et anagram ellers
                //forsøg at kombinere med flere ord via rekursion, hvis de ord der forsøges med nu ikke er for lange
                if ((worddepth == 1 && anagramLength == this.noOfCharsInAnagram) || (worddepth > 1 && anagramLength <= this.noOfCharsInAnagram))
                {
                    var anagram = new List<string>(ordDerSkalVæreMedIAnagram);
                    anagram.Add(ord);

                    var newtmpOkChars = CharsOk(ord, tmpOkChars);
                    if (newtmpOkChars != null)
                    {
                        if (anagramLength == this.noOfCharsInAnagram)
                        {
                            IEnumerable<IEnumerable<string>> permutationer = this.Permute(anagram);
                            var anagrams = permutationer.SelectMany(a => this.ConvertAlfabetizedAnagramToRealAnagrams(a));
                            this.AnagramFound(anagrams);
                        }
                        else if (worddepth > 1)
                        {
                            this.GetAnagrams(dictKeys, worddepth - 1, anagram, newtmpOkChars, anagramLength, i);
                        }
                    }
                }
            }
        }

        private void AnagramFound(IEnumerable<IEnumerable<string>> anagrams)
        {
            var anagramsAsStrings = anagrams.Select(a => string.Join(" ", a));
            var angramsAndMd5 = anagramsAsStrings.Select(a => new Anagram { Text = a, Md5 = CalculateMD5Hash(a) });
            this.anagrammer.AddRange(angramsAndMd5);
            this.totalFoundAnagrams += anagrams.Count();
            Dispatcher.Invoke(
                DispatcherPriority.Normal, 
                new Action(() => 
                {
                    antalAnagrammer.Content = totalFoundAnagrams;
                    sekunder.Content = (DateTime.Now - startTime).Seconds;
                }));

            foreach (var anagram in angramsAndMd5)
            {
                if (anagram.Md5 == "4624d200580677270a54ccff86b9610e")
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { ResultatAnagram.Content = anagram.Text; }));
                }
            }
        }
    }
}