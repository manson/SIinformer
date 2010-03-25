using System;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace SIinformer.Logic
{
    public class CategoryList : BindingList<Category>
    {
        public static CategoryList Load(string categoryFileName)
        {
            CategoryList result;
            try
            {
                using (var st = new StreamReader(categoryFileName))
                {
                    var sr = new XmlSerializer(typeof (CategoryList));
                    result = (CategoryList) sr.Deserialize(st);
                }
                foreach (Category category in result)
                {
                    category.SetOwner(result);
                }
                result.Reorder();
            }
            catch (Exception)
            {
                result = new CategoryList();
                Category category = new Category {Name = "Default", Index = 0};
                category.SetOwner(result);
                result.Add(category);
            }
            return result;
        }

        public void Save(string categoryFileName)
        {
            using (var st = new StreamWriter(categoryFileName))
            {
                var sr = new XmlSerializer(typeof (CategoryList));
                sr.Serialize(st, this);
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
            Category result = new Category {Name = name, Index = Count};
            result.SetOwner(this);
            Add(result);
            Reorder();
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
            foreach (Category category in Items)
            {
                if (category.Name == item) return true;
            }
            return false;
        }
    }
}