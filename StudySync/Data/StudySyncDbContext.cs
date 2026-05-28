using Microsoft.EntityFrameworkCore;
using StudySync.Models;

namespace StudySync.Data
{
    public class StudySyncDbContext : DbContext
    {
        public StudySyncDbContext(DbContextOptions<StudySyncDbContext> options)
            : base(options)
        {
        }

        // --------------------------------------------------------
        // DbSets - one per table
        // --------------------------------------------------------
        public DbSet<User> Users { get; set; }
        public DbSet<LearnerProfile> LearnerProfiles { get; set; }
        public DbSet<Interest> Interests { get; set; }
        public DbSet<LearnerProfileInterest> LearnerProfileInterests { get; set; }
        public DbSet<Partnership> Partnerships { get; set; }
        public DbSet<PartnershipFeedback> PartnershipFeedbacks { get; set; }
        public DbSet<RecommendationCache> RecommendationCaches { get; set; }
        public DbSet<CollaborationMessage> CollaborationMessages { get; set; }
        public DbSet<StudySession> StudySessions { get; set; }
        public DbSet<CollaborationFile> CollaborationFiles { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --------------------------------------------------------
            // Users table configuration
            // --------------------------------------------------------
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.UserID);

                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.HasIndex(u => u.MatriculationNumber)
                      .IsUnique();

                entity.Property(u => u.RegistrationDate)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(u => u.IsActive)
                      .HasDefaultValue(true);
            });

            // --------------------------------------------------------
            // LearnerProfiles - one-to-one with Users
            // --------------------------------------------------------
            modelBuilder.Entity<LearnerProfile>(entity =>
            {
                entity.ToTable("LearnerProfiles");
                entity.HasKey(lp => lp.UserID);

                entity.HasOne(lp => lp.User)
                      .WithOne(u => u.LearnerProfile)
                      .HasForeignKey<LearnerProfile>(lp => lp.UserID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(lp => lp.LastProfileUpdate)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(lp => lp.AvailabilityVector)
                      .HasDefaultValue(string.Empty);
            });

            // --------------------------------------------------------
            // Interests table
            // --------------------------------------------------------
            modelBuilder.Entity<Interest>(entity =>
            {
                entity.ToTable("Interests");
                entity.HasKey(i => i.InterestID);

                entity.HasIndex(i => i.InterestName)
                      .IsUnique();
            });

            // --------------------------------------------------------
            // LearnerProfileInterests - composite primary key (M:N)
            // --------------------------------------------------------
            modelBuilder.Entity<LearnerProfileInterest>(entity =>
            {
                entity.ToTable("LearnerProfileInterests");

                entity.HasKey(lpi => new { lpi.ProfileID, lpi.InterestID });

                entity.HasOne(lpi => lpi.LearnerProfile)
                      .WithMany(lp => lp.LearnerProfileInterests)
                      .HasForeignKey(lpi => lpi.ProfileID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(lpi => lpi.Interest)
                      .WithMany(i => i.LearnerProfileInterests)
                      .HasForeignKey(lpi => lpi.InterestID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // --------------------------------------------------------
            // Partnerships - two FKs to Users, no cascade to preserve history
            // --------------------------------------------------------
            modelBuilder.Entity<Partnership>(entity =>
            {
                entity.ToTable("Partnerships");
                entity.HasKey(p => p.PartnershipID);

                // Unique pair - no duplicate partnerships
                entity.HasIndex(p => new { p.User1ID, p.User2ID })
                      .IsUnique();

                entity.Property(p => p.Status)
                      .HasDefaultValue("Suggested");

                entity.Property(p => p.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(p => p.LastActivityAt)
                      .HasDefaultValueSql("GETDATE()");

                // User1 relationship - no cascade
                entity.HasOne(p => p.User1)
                      .WithMany(u => u.PartnershipAsUser1)
                      .HasForeignKey(p => p.User1ID)
                      .OnDelete(DeleteBehavior.NoAction);

                // User2 relationship - no cascade
                entity.HasOne(p => p.User2)
                      .WithMany(u => u.PartnershipAsUser2)
                      .HasForeignKey(p => p.User2ID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // --------------------------------------------------------
            // PartnershipFeedback
            // --------------------------------------------------------
            modelBuilder.Entity<PartnershipFeedback>(entity =>
            {
                entity.ToTable("PartnershipFeedback");
                entity.HasKey(pf => pf.FeedbackID);

                entity.Property(pf => pf.SubmittedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasOne(pf => pf.Partnership)
                      .WithMany(p => p.Feedbacks)
                      .HasForeignKey(pf => pf.PartnershipID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pf => pf.Giver)
                      .WithMany(u => u.FeedbackGiven)
                      .HasForeignKey(pf => pf.GiverUserID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // --------------------------------------------------------
            // RecommendationCache
            // --------------------------------------------------------
            modelBuilder.Entity<RecommendationCache>(entity =>
            {
                entity.ToTable("RecommendationCache");
                entity.HasKey(rc => rc.CacheID);

                entity.Property(rc => rc.ComputedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasOne(rc => rc.User)
                      .WithMany(u => u.RecommendationsCached)
                      .HasForeignKey(rc => rc.UserID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rc => rc.TargetUser)
                      .WithMany()
                      .HasForeignKey(rc => rc.TargetUserID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // CollaborationMessages
            modelBuilder.Entity<CollaborationMessage>(entity =>
            {
                entity.ToTable("CollaborationMessages");

                entity.HasOne(cm => cm.Partnership)
                      .WithMany()
                      .HasForeignKey(cm => cm.PartnershipID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cm => cm.Sender)
                      .WithMany()
                      .HasForeignKey(cm => cm.SenderUserID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // StudySessions
            modelBuilder.Entity<StudySession>(entity =>
            {
                entity.ToTable("StudySessions");

                entity.HasOne(ss => ss.Partnership)
                      .WithMany()
                      .HasForeignKey(ss => ss.PartnershipID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ss => ss.CreatedBy)
                      .WithMany()
                      .HasForeignKey(ss => ss.CreatedByUserID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<CollaborationFile>(entity =>
            {
                entity.ToTable("CollaborationFiles");

                entity.HasOne(cf => cf.Partnership)
                      .WithMany()
                      .HasForeignKey(cf => cf.PartnershipID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cf => cf.Uploader)
                      .WithMany()
                      .HasForeignKey(cf => cf.UploaderUserID)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("PasswordResetTokens");

                entity.HasOne(prt => prt.User)
                      .WithMany()
                      .HasForeignKey(prt => prt.UserID)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
