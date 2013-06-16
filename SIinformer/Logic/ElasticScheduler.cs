using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIinformer.Utils;

namespace SIinformer.Logic
{
    /// <summary>
    /// класс расчета гибкой проверки авторов
    /// </summary>
    public class ElasticScheduler
    {
        // кол-во дней с последней проверки, равное одному часу проверки
        private const int PerHourDays = 15;
        // макс. кол-во дней, соответствующих суточному диапазону проверки
        private const int MaxDaysPeriod = 720; // максимальный диапазон последней проды. 2 года

        private Logger _logger = null;
        private Setting _settings = null;
        private int _processedAuthors = 0;
        List<Tuple<string, int, DateTime>> authorsPlan = new List<Tuple<string, int, DateTime>>(); // рассчитанный план периодов обновлений. Для статистики.
        Dictionary<int, int> stat = new Dictionary<int, int>(); // статистика по периодам и кол-ву авторов

        public ElasticScheduler(Logger logger, Setting setting)
        {
            _logger = logger;
            _settings = setting;
            //_logger.Add("Расчет плана обновлений и статистики...", true);
        }
        /// <summary>
        /// высчитать следующую дату проверки для автора
        /// </summary>
        /// <param name="author"></param>
        public void MakePlan(Author author)
        {
            var authors = new List<Author> {author};
            MakePlan(authors);
        }
        /// <summary>
        /// высчитать следующую дату проверки для авторов
        /// </summary>
        /// <param name="authors"></param>
        public void MakePlan(List<Author> authors)
        {           
          
            foreach (var author in authors)
            {
                // кол-во дней с последнего обновления
                var days = new TimeSpan(DateTime.Now.Ticks - author.UpdateDate.Ticks).Days;
                days = days > MaxDaysPeriod ? MaxDaysPeriod : days;
                // высчитаем коэффициент. Он у нас не бывает больше 24 часов при PerHourDays = 15 и MaxDaysPeriod = 360. При других значениях он естественно другой
                int period = (int)Math.Round((double)days / (double)PerHourDays, MidpointRounding.AwayFromZero);
                period = period > 72 ? 72 : period; // на всякий случай, если поменяли MaxDaysPeriod. Не больше 3х суток разрыв
                period = period < 1 ? 1 : period; // не меньше одного часа
                var nextDate = (author.LastCheckDate == DateTime.MinValue ? DateTime.Now : author.LastCheckDate).AddHours(period) ;
                // проверим, если следующая дата проверки - завтра или дальше и время проверки редкое (минимум полсуток), то выставим ей час случайным образом, чтобы авторы не кучковались
                var tomorrowMidnight = DateTime.Today.AddDays(1);
                if (nextDate>=tomorrowMidnight && period>12)                
                    nextDate = new DateTime(nextDate.Year, nextDate.Month, nextDate.Day, new Random().Next(0,23), nextDate.Minute,0);
                author.NextCheckDate = nextDate;
                // сохранить статистику
                authorsPlan.Add(new Tuple<string, int, DateTime>(author.Name, period, author.NextCheckDate));
                if (!stat.ContainsKey(period))
                    stat.Add(period,0);
                stat[period]++;
                _processedAuthors++;
            }
           
            
        }
        /// <summary>
        /// сохранить статистику в виде екселовского файла
        /// </summary>
        /// <param name="stat"></param>
        /// <param name="authorsPlan"></param>
        public void SaveStatistics()
        {
            try
            {
                string finalMessage = string.Format("План следующей проверки рассчитан для {0} авторов", _processedAuthors);
                //_logger.Add(finalMessage);     
                if (_settings.SaveStatisticsOfElasticScheduler && _processedAuthors > 0)
                {
                    var statPath =
                        System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "Statistic");
                    if (!System.IO.Directory.Exists(statPath)) System.IO.Directory.CreateDirectory(statPath);
                    var statFile = System.IO.Path.Combine(statPath,
                                                          DateTime.Now.ToString().Replace(":", "").Replace(".", "-").Replace("\\", "-").Replace("/", "-") + ".svc");
                    using (var stream = System.IO.File.AppendText(statFile))
                    {
                        stream.WriteLine(finalMessage);
                        stream.WriteLine("");
                        stream.WriteLine("Период\tКол-во авторов");

                        foreach (var pair in stat.OrderBy(x => x.Key))
                            stream.WriteLine("{0}\t{1}", pair.Key, pair.Value);

                        stream.WriteLine("\t");
                        stream.WriteLine("Автор\tПериод обновления\tСледующая дата проверки");
                        foreach (var tuple in authorsPlan.OrderBy(t => t.Item2))
                            stream.WriteLine("{0}\t{1}\t{2}", tuple.Item1, tuple.Item2, tuple.Item3);
                        stream.Close();
                    }
                    if (_processedAuthors > 0)
                        _logger.Add(string.Format("Текущие план и статистика сохранены в файл {0}...", System.IO.Path.GetFileName(statFile)), true);
                }

            }
            catch (Exception ex)
            {
                _logger.Add(string.Format("Ошибка записи статистики в файл {0}...", ex), false,true);
            }
        }
    }
}
