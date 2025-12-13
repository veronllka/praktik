using System;
using System.Collections.Generic;

namespace praktik.Models
{
    /// <summary>
    /// Сущность "Заявка на материалы".
    /// Представляет запрос на получение материалов для выполнения задачи.
    /// Жизненный цикл управляется через паттерн State.
    /// </summary>
    public class MaterialRequest
    {
        public int RequestId { get; set; }
        
        /// <summary>
        /// ID задачи, для которой требуются материалы.
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// ID пользователя, создавшего заявку.
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>
        /// Дата создания заявки.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Желаемая дата получения материалов.
        /// </summary>
        public DateTime? RequiredDate { get; set; }

        /// <summary>
        /// Текущий статус заявки
        /// </summary>
        public string Status { get; set; } 

        /// <summary>
        /// Комментарий к заявке.
        /// </summary>
        public string Comment { get; set; }

        public Task Task { get; set; }
        public User CreatedByUser { get; set; }
        public List<MaterialRequestItem> Items { get; set; } = new List<MaterialRequestItem>();
    }
}

