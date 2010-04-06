using System.Windows.Data;
using System.Xml.Serialization;
using SIinformer.Utils;

namespace SIinformer.Logic
{
    public class Category : BindableObject
    {
        #region Private Fields

        private bool _collapsed = true;
        private int _index;
        private bool _isNew;
        private string _name = "Default";
        private string _visualName = "Default";
        private bool _isEmpty;

        #endregion

        #region Public Property

        /// <summary>
        /// Имя категории
        /// </summary>
        public string Name
        {
            get { return _name; }
            set
            {
                if (value != _name)
                {
                    _name = value;
                    RaisePropertyChanged("Name");
                }
            }
        }

        [XmlIgnore]
        public bool IsFirst
        {
            get { return Index == 0; }
        }

        [XmlIgnore]
        public bool IsLast
        {
            get { return Index == Owner.Count - 1; }
        }

        /// <summary>
        /// Признак пустой категории
        /// Вычисляется методом Category.SetVisualNameAndIsNew(AuthorList)
        /// </summary>
        [XmlIgnore]
        public bool IsEmpty
        {
            get { return _isEmpty; }
            private set
            {
                if (value != _isEmpty)
                {
                    _isEmpty = value;
                    RaisePropertyChanged("IsEmpty");
                }
            }
        }

        /// <summary>
        /// Наличие в категории новых авторов
        /// Вычисляется методом Category.SetVisualNameAndIsNew(AuthorList)
        /// </summary>
        [XmlIgnore]
        public bool IsNew
        {
            get { return _isNew; }
            private set
            {
                if (value != _isNew)
                {
                    _isNew = value;
                    RaisePropertyChanged("IsNew");
                }
            }
        }

        /// <summary>
        /// Визуальное представление имени категории с количеством авторов и новых авторов.
        /// Вычисляется методом Category.SetVisualNameAndIsNew(AuthorList)
        /// </summary>
        [XmlIgnore]
        public string VisualName
        {
            get { return _visualName; }
            private set
            {
                if (value != _visualName)
                {
                    _visualName = value;
                    RaisePropertyChanged("VisualName");
                }
            }
        }

        /// <summary>
        /// Состояние свернутости
        /// </summary>
        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (value != _collapsed)
                {
                    _collapsed = value;
                    RaisePropertyChanged("Collapsed");
                }
            }
        }

        [XmlIgnore]
        public int Index
        {
            get { return _index; }
            set
            {
                if (value != _index)
                {
                    _index = value;
                    RaisePropertyChanged("Index");
                    RaisePropertyChanged("IsFirst");
                }
                // при добавлении категории методом GetCategoryFromName и пересортировки списка,
                // не пересчитывалось IsLast, если внести RaisePropertyChanged внутрь условия, 
                // т.к. index последнего элемента не менялся.
                // поэтому теперь привязка IsLast обновляется всегда при задании index
                RaisePropertyChanged("IsLast");
            }
        }

        [XmlIgnore]
        public CategoryList Owner { get; private set; }

        #endregion

        #region Public Method

        public void SetOwner(CategoryList categoryList)
        {
            Owner = categoryList;
        }

        public void SetVisualNameAndIsNew(ListCollectionView authorList)
        {
            int counter = 0;
            int counterIsNew = 0;
            foreach (Author author in authorList)
            {
                if (author.Category == Name) counter++;
                if ((author.Category == Name) && (author.IsNew)) counterIsNew++;
            }
            VisualName = counterIsNew == 0
                             ? string.Format("{0} ({1})", Name, counter)
                             : string.Format("{0} ({1}/{2})", Name, counter, counterIsNew);
            IsNew = counterIsNew != 0;
            IsEmpty = counter == 0;
        }

        public void PositionUp()
        {
            if (Owner==null) return;
            var temp = Owner[Index];
            Owner[Index] = Owner[Index - 1];
            Owner[Index - 1] = temp;
            Owner.Reorder();
        }

        public void PositionDown()
        {
            if (Owner == null) return;
            var temp = Owner[Index];
            Owner[Index] = Owner[Index + 1];
            Owner[Index + 1] = temp;
            Owner.Reorder();
        }

        #endregion

        #region Override

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}