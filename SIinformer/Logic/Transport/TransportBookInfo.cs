using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer.Logic.Transport
{
    public class TransportBookInfo
    {
        public string CID { get; set; }// временный идентификатор пользователя. НЕ играет никакой роли, кроме как фильтровать сообщения, полученные по общей шине
        public string AuthorLink { get; set; }// линк проверяемого автора
        public string AuthorName { get; set; } // имя автора чисто для статистики сервера
        // параметры книги
        public string SectionName { get; set; }
        public string Description { get; set; }
        public string Genres { get; set; }
        public string Link { get; set; }
        public string Name { get; set; }
        public int Size { get; set; }
        public long UpdateDate { get; set; } // дату в формате числа, чтобы избежать проблем с передачей и преобразованиями
    }
}
