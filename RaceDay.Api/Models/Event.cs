using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema;



namespace RaceDay.Api.Models

{

    public class Event

    {

        [Key]

        public int EventId { get; set; }



        [Required]

        public int OrganiserId { get; set; }



        [ForeignKey("OrganiserId")]

        public User? Organiser { get; set; }



        [Required]

        [MaxLength(150)]

        public string Name { get; set; } = string.Empty;



        [Required]

        [MaxLength(20)]

        public string Discipline { get; set; } = string.Empty; // "Running", "Walking", "Cycling"



        [Required]

        public DateTime EventDate { get; set; }



        [Required]

        [MaxLength(150)]

        public string Location { get; set; } = string.Empty;



        [MaxLength(500)]

        public string? Description { get; set; }



        // Navigation properties

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<Route> Routes { get; set; } = new List<Route>();

    }

}