using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace one_armed_bandit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Random random = new Random();

        // Egyetlen érték, amivel csökkenthető vagy növelhető a tárcsákon lévő szimbólumok száma.
        // Teszteléshez pl. 4, végleges verzióhoz lehet 10, 15 vagy 20.
        int symbolCountOnReel = 9;

        string[] allSymbols =
        {
            "@", "#", "$", "%", "&", "*", "+", "7", "?"
        };

        int balance = 100;
        int spinCost = 10;

        string reel1FinalSymbol = "";
        string reel2FinalSymbol = "";
        string reel3FinalSymbol = "";

        public MainWindow()
        {
            InitializeComponent();

            UpdateBalanceText();
            ResultText.Text = "Eredmény: -";

            SetRandomStartSymbols();
        }

        private void SetRandomStartSymbols()
        {
            SetReelSymbols(Reel1Top, Reel1Middle, Reel1Bottom);
            SetReelSymbols(Reel2Top, Reel2Middle, Reel2Bottom);
            SetReelSymbols(Reel3Top, Reel3Middle, Reel3Bottom);
        }

        private async void SpinButton_Click(object sender, RoutedEventArgs e)
        {
            if (balance < spinCost)
            {
                ResultText.Text = "Nincs elég kredit!";
                return;
            }

            balance -= spinCost;
            UpdateBalanceText();

            ResultText.Text = "Pörgetés...";
            SpinButton.IsEnabled = false;

            // A három kerék különböző ideig pörög, ettől természetesebbnek tűnik.
            Task reel1Task = SpinReel(Reel1Top, Reel1Middle, Reel1Bottom, 20, 40, 1);
            Task reel2Task = SpinReel(Reel2Top, Reel2Middle, Reel2Bottom, 30, 45, 2);
            Task reel3Task = SpinReel(Reel3Top, Reel3Middle, Reel3Bottom, 40, 50, 3);

            await Task.WhenAll(reel1Task, reel2Task, reel3Task);

            CheckResult();

            SpinButton.IsEnabled = true;
        }

        private async Task SpinReel(TextBlock topSymbol, TextBlock middleSymbol, TextBlock bottomSymbol, int spinSteps, int delay, int reelNumber)
        {
            for (int i = 0; i < spinSteps; i++)
            {
                SetReelSymbols(topSymbol, middleSymbol, bottomSymbol);

                await Task.Delay(delay);
            }

            string finalSymbol = middleSymbol.Text;

            if (reelNumber == 1)
            {
                reel1FinalSymbol = finalSymbol;
            }
            else if (reelNumber == 2)
            {
                reel2FinalSymbol = finalSymbol;
            }
            else if (reelNumber == 3)
            {
                reel3FinalSymbol = finalSymbol;
            }
        }

        private void SetReelSymbols(TextBlock topSymbol, TextBlock middleSymbol, TextBlock bottomSymbol)
        {
            topSymbol.Text = GetRandomSymbol();
            middleSymbol.Text = GetRandomSymbol();
            bottomSymbol.Text = GetRandomSymbol();
        }

        private string GetRandomSymbol()
        {
            int randomIndex = random.Next(0, symbolCountOnReel);
            return allSymbols[randomIndex];
        }

        private void CheckResult()
        {
            if (reel1FinalSymbol == reel2FinalSymbol && reel2FinalSymbol == reel3FinalSymbol)
            {
                balance += 50;
                ResultText.Text = "WIN! +50 kredit";
            }
            else if (reel1FinalSymbol == reel2FinalSymbol ||
                     reel1FinalSymbol == reel3FinalSymbol ||
                     reel2FinalSymbol == reel3FinalSymbol)
            {
                balance += 20;
                ResultText.Text = "WIN! +20 kredit";
            }
            else
            {
                ResultText.Text = "LOSE!";
            }

            UpdateBalanceText();
        }

        private void UpdateBalanceText()
        {
            BalanceText.Text = "Egyenleg: " + balance + " kredit";
        }
    }
}