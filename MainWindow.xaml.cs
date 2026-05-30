using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace one_armed_bandit
{

    public partial class MainWindow : Window
    {
        Random random = new Random();
        MediaPlayer spinSoundPlayer = new MediaPlayer();
        MediaPlayer effectSoundPlayer = new MediaPlayer();

        bool reelsAreSpinning = false;

        /*int symbolCountOnReel = 4;*/
        int symbolCountOnReel = 7;

        /*string[] allSymbols =
        {
            "@", "#", "$", "%", "&", "*", "+", "7", "?"
        };*/

        int balance = 100;
        int spinCost = 10;
        int totalSpins = 0;
        int threeSymbolWins = 0;
        int twoSymbolWins = 0;
        int losses = 0;
        int totalPrizeCredits = 0;

        DateTime sessionStartTime;
        string statisticsFilePath = "";

        /*const double symbolHeight = 58;*/
        const double symbolHeight = 60;
        const double symbolImageHeight = 46;
        const double symbolImageWidth = 105;

        List<string> allSymbols = new List<string>();
        Dictionary<string, DrawingImage> symbolImages = new Dictionary<string, DrawingImage>();

        ReelState reel1;
        ReelState reel2;
        ReelState reel3;

        string reel1FinalSymbol = "";
        string reel2FinalSymbol = "";
        string reel3FinalSymbol = "";

        public MainWindow()
        {
            InitializeComponent();
            
            reel1 = new ReelState(Reel1Panel);
            reel2 = new ReelState(Reel2Panel);
            reel3 = new ReelState(Reel3Panel);

            UpdateBalanceText();
            ResultText.Text = "Result:";
            ResultTextResult.Text = "-";

            LoadSymbols();
            SetRandomStartSyms();

            CreateNewStatisticsFile();
            InitializeSounds();
        }


        private void LoadSymbols()
        {
            string assetsFolder = FindAssetsFolder();

            if (assetsFolder == "")
            {
                MessageBox.Show("The assets folder cannot be found.");
                SpinButton.IsEnabled = false;
                return;
            }

            allSymbols = Directory
                .GetFiles(assetsFolder, "slot-machine-*-symbol.svg")
                .OrderBy(filePath => filePath)
                .Take(symbolCountOnReel)
                .ToList();

            if (allSymbols.Count == 0)
            {
                MessageBox.Show("No SVG with that name found.: slot-machine-*-symbol.svg");
                SpinButton.IsEnabled = false;
                return;
            }

            foreach (string symbolPath in allSymbols)
            {
                symbolImages[symbolPath] = LoadSvg(symbolPath);
            }
        }


        private string FindAssetsFolder()
        {
            string currentFolder = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < 6; i++)
            {
                string possibleAssetsFolder = System.IO.Path.Combine(currentFolder, "assets");

                if (Directory.Exists(possibleAssetsFolder))
                {
                    return possibleAssetsFolder;
                }

                DirectoryInfo parentFolder = Directory.GetParent(currentFolder);

                if (parentFolder == null)
                {
                    break;
                }

                currentFolder = parentFolder.FullName;
            }

            return "";
        }


        private DrawingImage LoadSvg(string filePath)
        {
            WpfDrawingSettings settings = new WpfDrawingSettings();
            settings.IncludeRuntime = true;
            settings.TextAsGeometry = true;

            FileSvgReader reader = new FileSvgReader(settings);
            DrawingGroup drawing = reader.Read(filePath);

            DrawingImage image = new DrawingImage(drawing);
            image.Freeze();

            return image;
        }


        private string GetSoundFilePath(string fileName)
        {
            string assetsFolder = FindAssetsFolder();

            if (assetsFolder == "")
            {
                return "";
            }

            return System.IO.Path.Combine(assetsFolder, "sounds", fileName);
        }


        private void SetRandomStartSyms()
        {
            RandomSymbolsForReel(reel1);
            RandomSymbolsForReel(reel2);
            RandomSymbolsForReel(reel3);
        }

        private void RandomSymbolsForReel(ReelState reel)
        {
            for (int i = 0; i < reel.Symbols.Length; i++)
            {
                reel.Symbols[i] = GetRandomSymbol();
            }

            RefreshReelView(reel);           
        }

        private async void SpinButton_Click(object sender, RoutedEventArgs e)
        {
            if (balance < spinCost)
            {
                ResultText.Text = "Not enough credit!";
                ResultTextResult.Text = "";
                return;
            }

            balance -= spinCost;
            UpdateBalanceText();

            ResultText.Text = "Spinning...";
            ResultTextResult.Text = "-";

            SpinButton.IsEnabled = false;

            StartSpinSound();

            Task<string> reel1Task = SpinReel(reel1, 28, 35, 160);
            Task<string> reel2Task = SpinReel(reel2, 36, 35, 185);
            Task<string> reel3Task = SpinReel(reel3, 44, 35, 220);

            reel1FinalSymbol = await reel1Task;
            reel2FinalSymbol = await reel2Task;
            reel3FinalSymbol = await reel3Task;

            StopSpinSound();
            CheckResult();

            SpinButton.IsEnabled = true;
        }

        private async Task<string> SpinReel(ReelState reel, int spinSteps, int fastestDuration, int slowestDuration)
        {
            for (int i = 0; i < spinSteps; i++)
            {
                double progress = (double)i / (spinSteps - 1);
                double slowedProgress = progress * progress;
                int currentDuration = fastestDuration + (int)((slowestDuration - fastestDuration) * slowedProgress);

                string newSymbol = GetRandomSymbol();

                await AnimateOneSymbolStep(reel, newSymbol, currentDuration);
                reel.Symbols[2] = reel.Symbols[1];
                reel.Symbols[1] = reel.Symbols[0];
                reel.Symbols[0] = newSymbol;

                RefreshReelView(reel);
            }

            return reel.Symbols[1];
        }

        private Task AnimateOneSymbolStep(ReelState reel, string newSymbol, int duration)
        {
            TaskCompletionSource<bool> taskCompletion = new TaskCompletionSource<bool>();

            reel.MoveTransform.BeginAnimation(TranslateTransform.YProperty, null);

            reel.Panel.Children.Clear();

            reel.Panel.Children.Add(CreateSymbolImage(newSymbol));
            reel.Panel.Children.Add(CreateSymbolImage(reel.Symbols[0]));
            reel.Panel.Children.Add(CreateSymbolImage(reel.Symbols[1]));
            reel.Panel.Children.Add(CreateSymbolImage(reel.Symbols[2]));

            reel.MoveTransform.Y = -symbolHeight;

            DoubleAnimation animation = new DoubleAnimation();
            animation.From = -symbolHeight;
            animation.To = 0;
            animation.Duration = TimeSpan.FromMilliseconds(duration);
            animation.EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseInOut
            };

            animation.Completed += (sender, e) =>
            {
                taskCompletion.SetResult(true);
            };

            reel.MoveTransform.BeginAnimation(TranslateTransform.YProperty, animation);

            return taskCompletion.Task;
        }

        private void RefreshReelView(ReelState reel)
        {
            reel.MoveTransform.BeginAnimation(TranslateTransform.YProperty, null);
            reel.MoveTransform.Y = 0;

            reel.Panel.Children.Clear();

            for (int i = 0; i < reel.Symbols.Length; i++)
            {
                reel.Panel.Children.Add(CreateSymbolImage(reel.Symbols[i]));
            }

        }

        private Grid CreateSymbolImage(string symbolPath)
        {
            Grid symbolContainer = new Grid();

            symbolContainer.Height = symbolHeight;
            symbolContainer.Width = 110;

            Image image = new Image();
            image.Source = symbolImages[symbolPath];

            image.Height = symbolImageHeight;
            image.Width = symbolImageWidth;
            image.Stretch = Stretch.Uniform;
            image.VerticalAlignment = VerticalAlignment.Center;
            image.HorizontalAlignment = HorizontalAlignment.Center;

            symbolContainer.Children.Add(image);

            return symbolContainer;
        }

        private string GetRandomSymbol()
        {
            int randomIndex = random.Next(0, allSymbols.Count);
            return allSymbols[randomIndex];
        }

        private void CheckResult()
        {
            ResultText.Text = "Result:";

            totalSpins++;

            if (reel1FinalSymbol == reel2FinalSymbol && reel2FinalSymbol == reel3FinalSymbol)
            {
                balance += 50;
                threeSymbolWins++;
                totalPrizeCredits += 50;

                ResultTextResult.Text = "WIN! (+50)";

                PlayEffectSound("big_win.mp3");
            }
            else if (reel1FinalSymbol == reel2FinalSymbol ||
                     reel1FinalSymbol == reel3FinalSymbol ||
                     reel2FinalSymbol == reel3FinalSymbol)
            {
                balance += 20;
                twoSymbolWins++;
                totalPrizeCredits += 20;

                ResultTextResult.Text = "WIN! (+20)";
                
                PlayEffectSound("small_win.mp3");
            }
            else
            {
                losses++;

                ResultTextResult.Text = "LOSE!";
            }

            UpdateBalanceText();
            WriteStatisticsToFile();
        }

        private void UpdateBalanceText()
        {
            BalanceTextCredit.Text = balance + " credits";
        }

        private void CreateNewStatisticsFile()
        {
            sessionStartTime = DateTime.Now;

            string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
            string folderPath = System.IO.Path.Combine(projectDirectory, "./statistics");

            string fileName = "statistics_" + sessionStartTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
            statisticsFilePath = System.IO.Path.Combine(folderPath, fileName);

            WriteStatisticsToFile();
        }

        private double CalculatePercentage(int resultCount)
        {
            if (totalSpins == 0)
            {
                return 0;
            }

            return (double)resultCount / totalSpins * 100;
        }

        private void WriteStatisticsToFile()
        {
            int totalWins = threeSymbolWins + twoSymbolWins;
            int netResult = balance - 100;

            string[] statisticsText =
            {
                "ONE-ARMED-BANDIT - STATISTICS",
                "Start of the game: " + sessionStartTime.ToString("yyyy.MM.dd. HH:mm:ss"),
                "",
                "Total spins: " + totalSpins,
                "",
                "3 of a kind (+50 credits): " + threeSymbolWins + " piece(s) - " + CalculatePercentage(threeSymbolWins).ToString("0.00") + " %",
                "2 of a kind (+20 credits): " + twoSymbolWins + " piece(s) - " + CalculatePercentage(twoSymbolWins).ToString("0.00") + " %",
                "Lose \t:    " + losses + " piece(s) - " + CalculatePercentage(losses).ToString("0.00") + " %",
                "",
                "Total WINS: " + totalWins + " piece(s) - " + CalculatePercentage(totalWins).ToString("0.00") + " %",
                "Total Rewald: " + totalPrizeCredits + " credits",
                "Current balance: " + balance + " credits",
                "Net profit: " + netResult + " credits"
            };

            File.WriteAllLines(statisticsFilePath, statisticsText, Encoding.UTF8);
        }


        private void InitializeSounds()
        {
            string spinSoundPath = GetSoundFilePath("spinning-reel_2.mp3");

            if (File.Exists(spinSoundPath))
            {
                spinSoundPlayer.Open(new Uri(spinSoundPath, UriKind.Absolute));
                spinSoundPlayer.Volume = 0.45;
            }

            spinSoundPlayer.MediaEnded += SpinSoundPlayer_MediaEnded;
        }

        private void SpinSoundPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (reelsAreSpinning)
            {
                spinSoundPlayer.Position = TimeSpan.Zero;
                spinSoundPlayer.Play();
            }
        }

        private void StartSpinSound()
        {
            string spinSoundPath = GetSoundFilePath("spinning-reel_2.mp3");

            if (!File.Exists(spinSoundPath))
            {
                return;
            }

            reelsAreSpinning = true;

            spinSoundPlayer.Position = TimeSpan.Zero;
            spinSoundPlayer.Play();
        }

        private void StopSpinSound()
        {
            reelsAreSpinning = false;

            spinSoundPlayer.Stop();
            spinSoundPlayer.Position = TimeSpan.Zero;
        }

        private void PlayEffectSound(string fileName)
        {
            string soundPath = GetSoundFilePath(fileName);

            if (!File.Exists(soundPath))
            {
                return;
            }

            effectSoundPlayer.Stop();
            effectSoundPlayer.Close();

            effectSoundPlayer.Open(new Uri(soundPath, UriKind.Absolute));
            effectSoundPlayer.Volume = 0.65;
            effectSoundPlayer.Play();
        }
    }

        public class ReelState
    {
        public StackPanel Panel;
        public TranslateTransform MoveTransform;
        public string[] Symbols = new string[3];

        public ReelState(StackPanel panel)
        {
            Panel = panel;
            MoveTransform = new TranslateTransform();
            Panel.RenderTransform = MoveTransform;
        }
    }
}