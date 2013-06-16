using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SIinformer.Window;

namespace SIinformer.Logic
{
    public class CategoryList : BindingList<Category>
    {
        public bool IsDirty { get; set; }

        public CategoryList()
        {
            this.ListChanged += (s, e) => IsDirty = true;
            this.AddingNew += (s, e) => IsDirty = true;
        }

        public static CategoryList Load(string categoryFileName)
        {
            CategoryList result;
            if (!File.Exists(categoryFileName))
                result = LoadDefaultCategories();
            else
            try
            {
                // перегоняем файл в память (быстро)
                using (var fstream = new FileStream(categoryFileName, FileMode.Open, FileAccess.Read,
                                                           FileShare.ReadWrite))
                {
                    byte[] buffer = new byte[fstream.Length];
                    fstream.Read(buffer, 0, (int) fstream.Length);
                    using (var mstream = new MemoryStream(buffer))
                    {
                        // десериализируем (медленно)
                        using (var st = new StreamReader(mstream))
                        {
                            var sr = new XmlSerializer(typeof (CategoryList));
                            result = (CategoryList) sr.Deserialize(st);
                        }
                    }
                    fstream.Close();
                }
                foreach (Category category in result)
                {
                    category.SetOwner(result);
                }
                result.Reorder();
                result.IsDirty = false;
            }
            catch (Exception)
            {
                result = LoadDefaultCategories();                
            }

            return result;
        }

        static CategoryList LoadDefaultCategories()
        {
            var result =new CategoryList();
            var category = new Category { Name = "Default", Index = 0 };
            category.SetOwner(result);
            result.Add(category);
            result.IsDirty = true;
            return result;
        }

        

        public void Save(string categoryFileName)
        {
            if (!IsDirty) return;
            try
            {
                using (var mstream = new MemoryStream())
                {
                    using (var st = new StreamWriter(mstream))
                    {
                        var sr = new XmlSerializer(typeof (CategoryList));
                        sr.Serialize(st, this);
                        using (var file = new FileStream(categoryFileName, FileMode.Create, FileAccess.ReadWrite))
                        {
                            mstream.WriteTo(file);
                            file.Close();
                        }
                        st.Close();
                    }
                }
                IsDirty = false;
            }
            catch (Exception ex)
            {
                if (MainWindow.MainForm != null)
                {
                    var logger = MainWindow.MainForm.GetLogger();
                    if (logger != null)
                        MainWindow.MainForm.GetLogger()
                                  .Add("Ошибка записи категорий в файл: " + ex.Message, false, true);
                }
            }
        }

        /// <summary>
        /// Возвращает категорию по имени, создавая ее при необходимости в конце списка
        /// </summary>
        /// <param name="name">Имя категории</param>
        /// <returns>Категория</returns>
        public Category GetCategoryFromName(string name)
        {
            foreach (Category category in this)
            {
                if (category.Name.Trim() == name.Trim())
                    return category;
            }
            var result = new Category
                             {
                                 Name = name,
                                 Index = Count
                             }
                .SetOwner(this);
            Add(result);
            Reorder();
            IsDirty = true;
            return result;
        }

        public void Reorder()
        {
            for (int i = 0; i < Count; i++)
            {
                this[i].Index = i;
            }
        }

        public bool Contains(string item)
        {
            return Items.Any(category => category.Name == item);
        }
    }
}