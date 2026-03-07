namespace BlogApp.Models
{
    public class Comments
    {
        public int ID { get; set; }
        public string Content { get; set; }
        public string Author { get; set; }
        public DateTime CreateAt { get; set; }

        // Foreign Key
        public int PostId { get; set; }
        public BlogPost Post { get; set; }
    }
}
