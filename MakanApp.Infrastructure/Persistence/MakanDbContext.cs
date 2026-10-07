using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Persistence;

public sealed class MakanDbContext(DbContextOptions<MakanDbContext> options)
    : DbContext(options)
{
    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<MakanApp.Domain.Academic.Class> Classes => Set<MakanApp.Domain.Academic.Class>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<Session> AcademicSessions => Set<Session>();
    public DbSet<ScheduleRule> ScheduleRules => Set<ScheduleRule>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<AttendanceRevision> AttendanceRevisions => Set<AttendanceRevision>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentVersion> AssignmentVersions => Set<AssignmentVersion>();
    public DbSet<AssignmentRecipient> AssignmentRecipients => Set<AssignmentRecipient>();
    public DbSet<SubmissionAttempt> SubmissionAttempts => Set<SubmissionAttempt>();
    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();
    public DbSet<EvaluationRevision> EvaluationRevisions => Set<EvaluationRevision>();
    public DbSet<GradeRelease> GradeReleases => Set<GradeRelease>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamVersion> ExamVersions => Set<ExamVersion>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAttemptQuestion> ExamAttemptQuestions => Set<ExamAttemptQuestion>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<ConversationOwnershipTransfer> ConversationOwnershipTransfers =>
        Set<ConversationOwnershipTransfer>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRevision> MessageRevisions => Set<MessageRevision>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<MessageMention> MessageMentions => Set<MessageMention>();
    public DbSet<ConversationPin> ConversationPins => Set<ConversationPin>();
    public DbSet<MessagingChangeEvent> MessagingChangeEvents => Set<MessagingChangeEvent>();
    public DbSet<MessagingRealtimeOutboxMessage> MessagingRealtimeOutboxMessages =>
        Set<MessagingRealtimeOutboxMessage>();
    public DbSet<PersonalCommunicationGrant> PersonalCommunicationGrants =>
        Set<PersonalCommunicationGrant>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<AbuseReport> AbuseReports => Set<AbuseReport>();
    public DbSet<GuardianRelation> GuardianRelations => Set<GuardianRelation>();
    public DbSet<MakanApp.Domain.Organization.Organization> Organizations => Set<MakanApp.Domain.Organization.Organization>();
    public DbSet<OrganizationPerson> OrganizationPersons => Set<OrganizationPerson>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MakanDbContext).Assembly);
    }
}
