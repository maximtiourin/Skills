using Moserware.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication1 {
    class Program {
        private static StringBuilder sb = new StringBuilder();

        //Json QoL vars
        private static char _o = '{';
        private static char o_ = '}';
        private static char _a = '[';
        private static char a_ = ']';
        private static char c = ',';
        private static char e = '"';
        private static char k = ':';

        /*
         * Expected args:
         * executable t1=team1size t2=team2size team1Rank team2Rank {t1+t2 space seperated list of p=players of format: p.id p.mu p.sigma}
         *
         * team rank is just the index of their placement in the match, winner = 1, loser = 2, ties just require placements to be equal ie: 1, 1
         *
         * if p.mu and p.sigma are the string "?" instead of a double, then the player is new and will be given default mmr
         */
        static void Main(string[] args) {
            int argc = args.Length;
            int minargexpect = (isWindowsOS()) ? (4) : (5);

            if (argc < minargexpect) {
                Console.WriteLine("{\"error\": \"incorrect minimum amount of arguments expect " + minargexpect + "\"}");
                return;
            }

            int team1Size = int.Parse(args[minargexpect - 4]);
            int team2Size = int.Parse(args[minargexpect - 3]);
            int team1Rank = int.Parse(args[minargexpect - 2]);
            int team2Rank = int.Parse(args[minargexpect - 1]);

            int totalargexpect = minargexpect + (3 * (team1Size + team2Size));

            if (argc != totalargexpect) {
                Console.WriteLine("{\"error\": \"incorrect amount of arguments expect " + totalargexpect + "\"}");
                return;
            }

            //Set gameinfo for standard mmr
            GameInfo gameInfo = new GameInfo(
                DefaultInitialMean, DefaultInitialStandardDeviation, DefaultBeta, DefaultDynamicsFactor, DefaultDrawProbability);

            //Construct players
            const string unknown = "?";
            int playerIndex = 1;
            Dictionary<int, int> playerIds = new Dictionary<int, int>();
            Tuple<Player, Rating>[] players = new Tuple<Player, Rating>[team1Size + team2Size];
            for (int i = minargexpect; i < totalargexpect; i += 3) {
                string id = args[i];
                string mustr = args[i + 1];
                string sigmastr = args[i + 2];

                playerIds[playerIndex - 1] = int.Parse(id);

                Rating rating = null;
                if (mustr.Equals(unknown) || sigmastr.Equals(unknown)) {
                    rating = gameInfo.DefaultRating;
                }
                else {
                    double mu = double.Parse(mustr);
                    double sigma = double.Parse(sigmastr);
                    rating = new Rating(mu, sigma);
                }
                players[playerIndex - 1] = new Tuple<Player, Rating>(new Player(playerIndex), rating);

                playerIndex++;
            }

            //Construct teams
            Team<Player> team1 = new Team<Player>();
            Team<Player> team2 = new Team<Player>();
            double team1StartMMR = 0;
            double team2StartMMR = 0;
            for (int i = 0; i < team1Size; i++) {
                team1 = team1.AddPlayer(players[i].Item1, players[i].Item2);
                team1StartMMR += players[i].Item2.ConservativeRating;
            }
            for (int i = team1Size; i < team1Size + team2Size; i++) {
                team2 = team2.AddPlayer(players[i].Item1, players[i].Item2);
                team2StartMMR += players[i].Item2.ConservativeRating;
            }
            team1StartMMR = calculateMMR(team1StartMMR / team1Size);
            team2StartMMR = calculateMMR(team2StartMMR / team2Size);
            var teams = Teams.Concat(team1, team2);

            //Calculate results and match quality
            var results = TrueSkillCalculator.CalculateNewRatings(gameInfo, teams, team1Rank, team2Rank);
            var quality = TrueSkillCalculator.CalculateMatchQuality(gameInfo, teams);

            //Create json string
            s(startObj());

            //Match quality
            s(keynum("match_quality", quality));

            //Team mmrs (beginning and end of match)
            double team1EndMMR = 0;
            double team2EndMMR = 0;
            for (int i = 0; i < team1Size; i++) {
                team1EndMMR += results[players[i].Item1].ConservativeRating;
            }
            for (int i = team1Size; i < team1Size + team2Size; i++) {
                team2EndMMR += results[players[i].Item1].ConservativeRating;
            }
            team1EndMMR = calculateMMR(team1EndMMR / team1Size);
            team2EndMMR = calculateMMR(team2EndMMR / team2Size);

            s(startObj("team_ratings"));

            //Initial ratings
            s(startArr("initial"));

            //Team 1 initial
            s(startObj());

            s(keynum("mmr", (int) team1StartMMR, false));

            s(endObj());

            //Team 2 initial
            s(startObj());

            s(keynum("mmr", (int) team2StartMMR, false));

            s(endObj(false));

            s(endArr());

            //Final ratings
            s(startArr("final"));

            //Team 1 final
            s(startObj());

            s(keynum("mmr", (int) team1EndMMR, false));

            s(endObj());

            //Team 2 final
            s(startObj());

            s(keynum("mmr", (int) team2EndMMR, false));

            s(endObj(false));

            s(endArr(false));

            s(endObj());

            //Player array
            s(startArr("players"));

            int pcount = players.Length;
            int p = 0;
            foreach (var tuple in players) {
                Player player = tuple.Item1;
                Rating newRating = results[player];

                //Player object
                s(startObj());

                //Id (passed in player id)
                s(keynum("id", playerIds[p]));

                //MMR (conservative rating of mu - (3 * sigma))
                s(keynum("mmr", calculateMMR(newRating.ConservativeRating)));

                //Mu (mean)
                s(keynum("mu", newRating.Mean));

                //Sigma (std dev)
                s(keynum("sigma", newRating.StandardDeviation, false));

                s(endObj(p < pcount - 1));

                p++;
            }

            s(endArr(false));

            s(endObj(false));

            //Output json
            Console.WriteLine(sb);
        }

        private static void s(char v) {
            sb.Append(v);
        }

        private static void s(string v) {
            sb.Append(v);
        }

        private static string str(string v) {
            return e + v + e;
        }

        private static string seperate(bool append = true) {
            if (append) return c + "";
            else return "";
        }

        private static string keystr(string key, string v, bool commaTerminate = true) {
            return str(key) + k + str(v) + seperate(commaTerminate);
        }

        private static string keynum(string key, int v, bool commaTerminate = true) {
            return str(key) + k + v + seperate(commaTerminate);
        }

        private static string keynum(string key, long v, bool commaTerminate = true) {
            return str(key) + k + v + seperate(commaTerminate);
        }

        private static string keynum(string key, float v, bool commaTerminate = true) {
            return str(key) + k + v + seperate(commaTerminate);
        }

        private static string keynum(string key, double v, bool commaTerminate = true) {
            return str(key) + k + v + seperate(commaTerminate);
        }

        private static string startObj() {
            return _o + "";
        }

        private static string startObj(string key) {
            return str(key) + k + _o;
        }

        private static string endObj(bool commaTerminate = true) {
            return o_ + seperate(commaTerminate);
        }

        private static string startArr(string key) {
            return str(key) + k + _a;
        }

        private static string endArr(bool commaTerminate = true) {
            return a_ + seperate(commaTerminate);
        }

        private static List<T> filterToList<T>(IEnumerable<T> e, Func<T, bool> predicate) {
            List<T> list = new List<T>();
            foreach (var n in e) {
                if (predicate(n)) list.Add(n);
            }
            return list;
        }

        private static bool isWindowsOS() {
            switch (Environment.OSVersion.Platform) {
                case PlatformID.WinCE:
                    return true;
                case PlatformID.Win32Windows:
                    return true;
                case PlatformID.Win32S:
                    return true;
                case PlatformID.Win32NT:
                    return true;
                default:
                    return false;
            }
        }

        private static int calculateMMR(double conservativeRating) {
            return Math.Max(0, (int) (conservativeRating * MMRScalar));
        }

        private const double DefaultBeta = DefaultInitialMean / 6.0;
        private const double DefaultDrawProbability = 0.0; //D = 0.10, but set to 0 because hots games can't draw
        private const double DefaultDynamicsFactor = DefaultInitialMean / 300.0;
        private const double DefaultInitialMean = 25.0; //D = 25.0, baseline mmr = 2000
        private const double DefaultInitialStandardDeviation = DefaultInitialMean / 3.0;
        private const int MMRScalar = 80; //Scale conservative rating by this value to get a normalized conservative mmr on [0, 4000]. To calculate new ranges, divide the max range by 50 ie: 4000 / 50 = 80;
        private const int MMRClampMin = 0; //Mmr can't go below 0
        private const int MMRClampMax = ((int) DefaultInitialMean) * 2 * MMRScalar; //Mmmr max is 2 * initial mean * mmrscalar, currently not used.
    }
}
