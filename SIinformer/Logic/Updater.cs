using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading;
using SIinformer.Readers;
using SIinformer.Utils;

namespace SIinformer.Logic
{
    public class Updater
    {
        private readonly Logger _logger;
        private Setting _setting;
        private readonly SynchronizationContext _syncContext;
        private readonly BackgroundWorker _worker;
        private string _baloonInfo;
        private ManualResetEvent _manualEvent;

        #region Public Method

        public Updater(Setting setting, Logger service)
        {
            _syncContext = SynchronizationContext.Current;
            _logger = service;
            _setting = setting;

            _worker = new BackgroundWorker {WorkerSupportsCancellation = true};
            _worker.DoWork += WorkerDoWork;
            _worker.RunWorkerCompleted += WorkerRunWorkerCompleted;
        }

       
        public bool IsBusy
        {
            get { return _worker.IsBusy; }
        }

        public bool ManualUpdater { get; set; }

        public void CancelAsync()
        {
            _worker.CancelAsync();
        }

        public void RunWorkerAsync(List<Author> updatedAuthor)
        {
            //new ElasticScheduler(_logger, _setting).MakePlan(updatedAuthor);
            _baloonInfo = "";
            //if (!ManualUpdater)
                _logger.Working = true;
            _worker.RunWorkerAsync(updatedAuthor);
        }

        public event RunWorkerCompletedEventHandler UpdaterComplete;

        #endregion

        private void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            App.BalanceInterval = 0;// обнуляем стартовый баланс
            //if (!ManualUpdater)
                _logger.Working = false;
            if (e.Cancelled)
                _logger.Add("Проверка прервана: " + DateTime.Now);
            else _logger.Add("Проверка выполнена: " + DateTime.Now);
            if (UpdaterComplete != null)
                UpdaterComplete(this, new RunWorkerCompletedEventArgs(_baloonInfo, e.Error, e.Cancelled));
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {

            var elasticScheduler = new ElasticScheduler(_logger, _setting);
            var list = (List<Author>) e.Argument;

            int index = 1;
            int authorsCnt = list.Count;
            foreach (Author author in list)
            {
                if (_worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                SyncRun(Action.IsUpdaterTrue, author);
                try
                {
                    SyncRun(Action.SetStatus,
                            new SetStatusParam
                                {
                                    Message =
                                        string.Format("{1}/{2}: '{0}' проверяется с родного сайта", author.Name, index, list.Count),
                                    ToMessage = true,
                                    IsError = false
                                });
                    string page = author.GetAuthorPage();
                    if (page != null)
                    {
                        author.DaysInaccessible = 0;// скидываем накопительный счетчик дней недоступности, так как страничка доступна стала.
                        SyncRun(Action.UpdateAuthorText, new UpdateTextParam(author, page, index, list.Count));

                    }
                    else
                    {
                        // проанализируем недоступность во времени
                        var dayDate = new DateTime(author.LastCheckDate.Year, author.LastCheckDate.Month,
                                                   author.LastCheckDate.Day);
                        if (dayDate < DateTime.Today) // если последний раз проверял не сегодня и сегодня недоступен, увеличиваем маркер недоступности. То есть если сегодня уже был недоступен, то этот факт игнорируем. Таким образом мы раз в сутки учитываем недоступность                        
                            author.DaysInaccessible = author.DaysInaccessible+1; // увеличиваем счетчик недоступности дней, там автор сам уйдет в игнор, если счетчик превысит константу _maxDaysInaccessibility
                            
                        
                        SyncRun(Action.SetStatus,
                                new SetStatusParam
                                    {
                                        Message = string.Format("Недоступна страница '{0}'. {1}", author.Name, author.IsIgnored ? "Автор отключен от проверок из-за превышения кол-ва дней недоступности." : ""),
                                        ToMessage = true,
                                        IsError = true
                                    });
                    }
                }
                catch (Exception ex)
                {
                    SyncRun(Action.SetStatus,
                            new SetStatusParam
                                {
                                    Message = ex.StackTrace,
                                    ToMessage = false,
                                    IsError = true
                                });
                    SyncRun(Action.SetStatus,
                            new SetStatusParam
                                {
                                    Message = ex.Message,
                                    ToMessage = true,
                                    IsError = true
                                });
                    SyncRun(Action.SetStatus,
                            new SetStatusParam
                                {
                                    Message =
                                        string.Format("{1}/{2}: '{0}' не проверен. Ошибка.", author.Name, index,
                                                      list.Count),
                                    ToMessage = true,
                                    IsError = true
                                });
                }

                SyncRun(Action.IsUpdaterFalse, author);
                
                // пропишем дату последней проверки автора. Необходимо для рассчета следующего времени проверки
                author.LastCheckDate = DateTime.Now;
                // перерасчитаем следующее время проверки автора               
                try
                {
                    elasticScheduler.MakePlan(author);
                }
                catch (Exception ex)
                {
                    SyncRun(Action.SetStatus,
                              new SetStatusParam
                              {
                                  Message =
                                      string.Format("Ошибка формирования плана проверок автора {0} - {1}",
                                                    author.Name, ex),
                                  ToMessage = true,
                                  IsError = true
                              });
                }

                index++;
                // задержка, если проверка больше одного автора
                if (authorsCnt > 1)
                {
                    var period = _setting.IntervalOfUpdate*3600; // весь период обновлений
                    var rnd = new Random();
                    var waitSpan = period/authorsCnt;
                    if (App.BalanceInterval > 0)
                        waitSpan = App.BalanceInterval;
                    else
                    {
                        waitSpan = waitSpan < 10 ? rnd.Next(10, 15) : waitSpan;
                        waitSpan = waitSpan > 15 ? 15 : waitSpan;
                    }
                    while (waitSpan > 0)
                    {
                        SyncRun(Action.SetStatus,
                                new SetStatusParam
                                    {
                                        Message =
                                            string.Format("->Балансировка нагрузки: ожидание {0} секунд(ы)...", waitSpan),
                                        ToMessage = true,
                                        IsError = false
                                    });
                        if (_worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                        Thread.Sleep(1000);
                        waitSpan--;
                    }
                }
            }
            // вызываем метод сохранения статистики. Однако внутри него он ориентируется на настройку "Сохранять статистику"
            try
            {
                elasticScheduler.SaveStatistics();
            }
            catch (Exception ex)
            {
                SyncRun(Action.SetStatus,
                        new SetStatusParam
                            {
                                Message =
                                    string.Format("Ошибка сохранение статистики. {0}", ex),
                                ToMessage = true,
                                IsError = true
                            });
            }
            var cachedAuthors = new List<Author>();
            var cachedAuthorTexts = new List<AuthorText>();
            foreach (Author author in list)
            {
                foreach (AuthorText authorText in author.Texts)
                {
                    if (string.IsNullOrWhiteSpace(authorText.Name)) continue;
                    authorText.UpdateIsCached(author);
                    if ((authorText.IsNew) && (!authorText.IsCached))
                    {
                        if ((authorText.Cached == true) || ((authorText.Cached == null) && (author.Cached == true)) ||
                            ((authorText.Cached == null) && ((author.Cached == null) && _setting.Cached)))
                        {
                            cachedAuthors.Add(author);
                            cachedAuthorTexts.Add(authorText);
                        }
                    }
                }
            }
            if (cachedAuthors.Count > 0)
            {
                SyncRun(Action.SetStatus,
                        new SetStatusParam
                            {
                                Message = string.Format("Кешируется {0} книг", cachedAuthors.Count),
                                ToMessage = true,
                                IsError = false
                            });
                for (int i = 0; i < cachedAuthors.Count; i++)
                {
                    SyncRun(Action.SetStatus,
                            new SetStatusParam
                                {
                                    Message =
                                        string.Format("{1}/{2}: Кешируется '{0}'", cachedAuthorTexts[i].Name, i + 1,
                                                      cachedAuthors.Count),
                                    ToMessage = true,
                                    IsError = false
                                });
                    var cachedParam = new CachedParam
                                                  {Author = cachedAuthors[i], AuthorText = cachedAuthorTexts[i]};
                    SyncRun(Action.CachedAdd, cachedParam);
                    cachedParam.DownloadTextItem.DownloadTextComplete += ItemDownloadTextComplete;

                    _manualEvent = new ManualResetEvent(false);
                    // таймаут на закачку книги исходя из скорости 28 кбит/сек с учетом размера книги
                    int timeout = Math.Max(60*1000, cachedAuthorTexts[i].Size*60*1000/210);
                    bool result = _manualEvent.WaitOne(timeout);
                    cachedParam.DownloadTextItem.DownloadTextComplete -= ItemDownloadTextComplete;
                    if (!result)
                    {
                        SyncRun(Action.SetStatus,
                                new SetStatusParam
                                    {
                                        Message =
                                            string.Format("{1}/{2}: Закачка книги '{0}' прервана по таймауту",
                                                          cachedAuthorTexts[i].Name, i + 1,
                                                          cachedAuthors.Count),
                                        ToMessage = true,
                                        IsError = true
                                    });
                        SyncRun(Action.CachedRemove, cachedParam);
                    }
                }
            }
        }

        private void ItemDownloadTextComplete(DownloadTextItem sender, DownloadDataCompletedEventArgs args)
        {
            _manualEvent.Set();
        }

        /// <summary>
        /// Выполняет действие в контексте gui потока
        /// </summary>
        /// <param name="action">Действие</param>
        /// <param name="body">Параметры действия</param>
        private void SyncRun(Action action, object body)
        {
            switch (action)
            {
                case Action.UpdateAuthorText:
                    {
                        var updateTextParam = (UpdateTextParam) body;
                        updateTextParam.IsNew = updateTextParam.Author.UpdateAuthorInfo(updateTextParam.Page, _syncContext, _setting.SkipBookDescription);
                        _syncContext.Post(SyncRun, new RunContent(action, updateTextParam));
                    }
                    break;
                case Action.CachedAdd: //синхронный, т.к. надо получить ответ в CachedParam.DownloadTextItem
                    _syncContext.Send(SyncRun, new RunContent(action, body));
                    break;
                default:
                    _syncContext.Post(SyncRun, new RunContent(action, body));
                    break;
            }
        }

        private void SyncRun(object state)
        {
            Action action = ((RunContent) state).Action;
            object body = ((RunContent) state).Body;
            switch (action)
            {
                case Action.SetStatus:
                    var param = (SetStatusParam) body;
                    _logger.Add(param.Message, param.ToMessage, param.IsError);
                    break;
                case Action.IsUpdaterTrue:
                    ((Author) body).IsUpdated = true;
                    break;
                case Action.IsUpdaterFalse:
                    ((Author) body).IsUpdated = false;
                    break;
                case Action.UpdateAuthorText:
                    var updateTextParam = (UpdateTextParam) body;
                    if (updateTextParam.IsNew)
                    {
                        _logger.Add(string.Format("{1}/{2}: '{0}' обновлен", updateTextParam.Author.Name,
                                                  updateTextParam.Index, updateTextParam.Count));
                        if (_baloonInfo == "") _baloonInfo = "Обновились авторы:\n";
                        _baloonInfo = _baloonInfo + updateTextParam.Author.Name + "; ";
                    }
                    break;
                case Action.CachedAdd:
                    var cachedParamAdd = (CachedParam) body;
                    DownloadTextItem item = DownloadTextHelper.Add(cachedParamAdd.Author, cachedParamAdd.AuthorText);
                    if (item.Text == null)
                    {
                        item.Start();
                    }
                    cachedParamAdd.DownloadTextItem = item;
                    break;
                case Action.CachedRemove:
                    var cachedParamRemove = (CachedParam) body;
                    cachedParamRemove.DownloadTextItem.Stop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("state");
            }
        }

        #region Nested type: Action

        internal enum Action
        {
            SetStatus,
            IsUpdaterTrue,
            IsUpdaterFalse,
            UpdateAuthorText,
            CachedAdd,
            CachedRemove,
        }

        #endregion

        #region Nested type: CachedParam

        internal class CachedParam
        {
            internal Author Author { get; set; }
            internal AuthorText AuthorText { get; set; }
            internal DownloadTextItem DownloadTextItem { get; set; }
        }

        #endregion

        #region Nested type: RunContent

        internal class RunContent
        {
            internal RunContent(Action action, object body)
            {
                Action = action;
                Body = body;
            }

            internal Action Action { get; set; }
            internal object Body { get; set; }
        }

        #endregion

        #region Nested type: SetStatusParam

        internal class SetStatusParam
        {
            internal string Message { get; set; }
            internal bool ToMessage { get; set; }
            internal bool IsError { get; set; }
        }

        #endregion

        #region Nested type: UpdateTextParam

        internal class UpdateTextParam
        {
            internal UpdateTextParam(Author author, string page, int index, int count)
            {
                Author = author;
                Page = page;
                Index = index;
                Count = count;
            }

            internal bool IsNew { get; set; }
            internal Author Author { get; set; }
            internal string Page { get; set; }
            internal int Index { get; set; }
            internal int Count { get; set; }
        }

        #endregion
    }
}