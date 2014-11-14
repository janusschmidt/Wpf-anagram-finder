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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace findkaninen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class Anagram
        {
            public string anagram { get; set; }
            public string md5 { get; set; }
        }

        public List<Anagram> anagrammer = new List<Anagram>();
        int noOfCharsInAnagram;
        List<char> OKchars;
        Dictionary<string, List<string>> alfabetiseretListe;
        int totalFoundAnagrams;
        DateTime startTime;

        public MainWindow()
        {
            InitializeComponent();
        }

        bool charsOk(IEnumerable<string> str)
        {
            return charsOk(string.Join("", str));
        }

        bool charsOk(string str)
        {
            return charsOk(str.ToCharArray());
        }

        bool charsOk(char[] charsToCheck)
        {
            bool res = true;
            var okcharstmp = OKchars.ToList();

            if (charsToCheck.Length == 0)
            {
                return false;
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
                    res = false;
                    break;
                }
            }
            return res;
        }

        string CalculateMD5Hash(string input)
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

        async void Button_Click(object sender, RoutedEventArgs e)
        {
            var phrase = subjectphrase.Text;
            await Task.Run(() => { start(phrase); });
        }

        void start(string phrase)
        {
            string[] dict = initFindAnagrams(phrase);

            //find ok words
            //remove whitespace and duplicates and all words that has letters not in subject or more of one letter than in subject
            var oklistwords = dict.Select(w => Regex.Replace(w, @"\s", "")).GroupBy(w => w).Where(g => charsOk(g.Key)).Select(g => g.Key);

            //lav alfabetiseret liste
            alfabetiseretListe = lavAlfabetiseretOrdbog(oklistwords);


            getAnagrams(alfabetiseretListe.Keys.ToArray(), 3, new List<string>());

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { anagramgrid.ItemsSource = anagrammer.OrderByDescending(a => a.anagram); }));
        }

        private string[] initFindAnagrams(string phrase)
        {
            startTime = DateTime.Now;
            totalFoundAnagrams = 0;
            anagrammer.Clear();
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { anagramgrid.ItemsSource = null; }));


            OKchars = phrase.Replace(" ", "").ToCharArray().ToList();
            noOfCharsInAnagram = OKchars.Count;
            return File.ReadAllLines("wordlist.txt");
        }

        Dictionary<string, List<string>> lavAlfabetiseretOrdbog(IEnumerable<string> oklistwords)
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

        private IEnumerable<IEnumerable<string>> convertAlfabetizedAnagramToRealAnagrams(IEnumerable<string> anagramIN)
        {
            var anagram = new Stack<string>(anagramIN);

            if (anagram.Count > 0)
            {
                var word = anagram.Pop();
                var alphabetizisedEquivalents = alfabetiseretListe[word];
                if (anagram.Count == 0)
                {
                    foreach (var eqWord in alphabetizisedEquivalents)
                    {
                        yield return new[] { eqWord };
                    }
                }
                else
                {
                    foreach (var subAnagrams in convertAlfabetizedAnagramToRealAnagrams(anagram))
                    {
                        foreach (var eqWord in alphabetizisedEquivalents)
                        {
                            yield return subAnagrams.Concat(new[] { eqWord });
                        }
                    }
                }
            }

        }

        private IEnumerable<List<string>> permute(List<string> anagram)
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
                    foreach (var subperm in permute(subanagram))
                    {
                        subperm.Add(word);
                        yield return subperm;
                    }
                }
            }
        }

        void getAnagrams(string[] dictKeys, int worddepth, List<string> ordDerSkalVæreMedIAnagram, int ordDerSkalVæreMedLength = 0, int dictStartPos = 0)
        {
            for (var i = dictStartPos; i < dictKeys.Length; i++)
            {
                var ord = dictKeys[i];
                var anagramLength = ordDerSkalVæreMedLength + ord.Length;
                if ((worddepth == 1 && anagramLength == noOfCharsInAnagram) || (worddepth > 1 && anagramLength <= noOfCharsInAnagram))
                {
                    var anagram = new List<string>(ordDerSkalVæreMedIAnagram);
                    anagram.Add(ord);

                    if (charsOk(anagram))
                    {
                        if (anagramLength == noOfCharsInAnagram)
                        {
                            IEnumerable<IEnumerable<string>> permutationer = permute(anagram);
                            var anagrams = permutationer.SelectMany(a => convertAlfabetizedAnagramToRealAnagrams(a));
                            anagramFound(anagrams);
                        }
                        //forsøg at kombinere med flere ord via rekursion, 
                        //hvis de ord der forsøges med nu ikke er for lange
                        else if (worddepth > 1)
                        {
                            getAnagrams(dictKeys, worddepth - 1, anagram, anagramLength, i);
                        }
                    }
                }
            }
        }

        void anagramFound(IEnumerable<IEnumerable<string>> anagrams)
        {
            var anagramsAsStrings = anagrams.Select(a => String.Join(" ", a));
            var angramsAndMd5 = anagramsAsStrings.Select(a => new Anagram { anagram = a, md5 = CalculateMD5Hash(a) });
            anagrammer.AddRange(angramsAndMd5);
            totalFoundAnagrams += anagrams.Count();
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                antalAnagrammer.Content = totalFoundAnagrams;
                sekunder.Content = (DateTime.Now - startTime).Seconds;
            }));


            foreach (var anagram in angramsAndMd5)
            {
                if (anagram.md5 == "4624d200580677270a54ccff86b9610e")
                {
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { ResultatAnagram.Content = anagram.anagram; }));
                }
            }
        }
    }
}