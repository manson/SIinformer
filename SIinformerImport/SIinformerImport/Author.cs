using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer
{   
    public class Author: BindableObjectLibrary.BindableObject
    {
        public string Name { get; set; }
        public string URL { get; set; }
        private DateTime updateDate = DateTime.Now;
        public DateTime UpdateDate
        {
            get { return updateDate; }
            set
            {
                if (value == updateDate)
                    return;

                updateDate = value;

                base.RaisePropertyChanged("UpdateDate");
                base.RaisePropertyChanged("UpdateDateVisual");
            }
        }

        public string UpdateDateVisual
        {
            get { return "Обновлено: " + updateDate.ToShortDateString(); }
        }

        private bool isNew = false;
        public bool IsNew
        {
            get { return isNew; }
            set
            {
                if (value == isNew)
                    return;

                isNew = value;

                base.RaisePropertyChanged("IsNew");
                base.RaisePropertyChanged("Star");
            }

        }
        public MySortableBindingList<AuthorText> Texts = new MySortableBindingList<AuthorText>();
        public string Star
        {
            get
            {
                if (IsNew)
                    return "pack://application:,,,/Resources/star_yellow_new16.png";//global::SIinformer.Properties.Resources.star_yellow_new16;
                else
                    return "pack://application:,,,/Resources/star_grey16.png";//global::SIinformer.Properties.Resources.star_grey16;
            }
        }
    
    }

    /// <summary>
    /// Детальная информация на страничке автора по произведению
    /// </summary>
    public class AuthorText : BindableObjectLibrary.BindableObject
    {
        public int Order { get; set; }
        public string SectionName { get; set; }
        public string Description { get; set; }
        public string Genres { get; set; }
        public string Link { get; set; }
        public string Name { get; set; }
        public int Size { get; set; }
        private bool isNew = false;
        public bool IsNew
        {
            get { return isNew; }
            set
            {
                if (value == isNew)
                    return;

                isNew = value;

                base.RaisePropertyChanged("IsNew");
                base.RaisePropertyChanged("Star");
            }

        }
        public string Star
        {
            get
            {
                if (IsNew)
                    return "pack://application:,,,/Resources/star_yellow_new16.png";//global::SIinformer.Properties.Resources.star_yellow_new16;
                else
                    return "pack://application:,,,/Resources/star_grey16.png";//global::SIinformer.Properties.Resources.star_grey16;
            }
        }

    }

    public class StatusMessageHolder : BindableObjectLibrary.BindableObject
    {
        private string message = "";
        public string Message { get { return message; } 
            set {
                if (value == message)
                    return;

                message = value;
                base.RaisePropertyChanged("Message");        
        } }

        private bool working = false;
        public bool Working
        {
            get { return working; }
            set
            {
                if (value == working)
                    return;

                working = value;
                base.RaisePropertyChanged("Working");
            }
        }


    }
}
