using Dapper;
using System.Data;

namespace EduPlatform.Data;

public static class DbInitializer
{
    public static async Task EnsureCreatedAsync(IDbConnection db)
    {
        const string sql = """
            IF COL_LENGTH('Courses', 'Description') IS NULL
                ALTER TABLE Courses ADD Description NVARCHAR(500) NULL;

            IF OBJECT_ID('Enrollments', 'U') IS NULL
            BEGIN
                CREATE TABLE Enrollments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    CourseId INT NOT NULL,
                    StudentId INT NOT NULL,
                    EnrolledAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_Enrollments_Courses FOREIGN KEY (CourseId) REFERENCES Courses(Coure_ID) ON DELETE CASCADE,
                    CONSTRAINT FK_Enrollments_Users FOREIGN KEY (StudentId) REFERENCES users(id) ON DELETE CASCADE,
                    CONSTRAINT UQ_Enrollment UNIQUE (CourseId, StudentId)
                );
            END

            IF OBJECT_ID('Assignments', 'U') IS NULL
            BEGIN
                CREATE TABLE Assignments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    CourseId INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(1000) NULL,
                    DueDate DATETIME NULL,
                    MaxGrade INT NOT NULL DEFAULT 100,
                    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT FK_Assignments_Courses FOREIGN KEY (CourseId) REFERENCES Courses(Coure_ID) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID('Submissions', 'U') IS NULL
            BEGIN
                CREATE TABLE Submissions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AssignmentId INT NOT NULL,
                    StudentId INT NOT NULL,
                    OriginalFileName NVARCHAR(260) NOT NULL,
                    StoredFileName NVARCHAR(260) NOT NULL,
                    SubmittedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                    Grade INT NULL,
                    Feedback NVARCHAR(1000) NULL,
                    GradedAt DATETIME NULL,
                    GradedBy INT NULL,
                    CONSTRAINT FK_Submissions_Assignments FOREIGN KEY (AssignmentId) REFERENCES Assignments(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_Submissions_Students FOREIGN KEY (StudentId) REFERENCES users(id),
                    CONSTRAINT UQ_Submission UNIQUE (AssignmentId, StudentId)
                );
            END

            IF NOT EXISTS (SELECT 1 FROM users WHERE username = 'teacher')
            BEGIN
                INSERT INTO users (username, password_hash, role_id, created_at)
                VALUES ('teacher', 'teacher123', 1, GETUTCDATE());
            END
            """;

        await db.ExecuteAsync(sql);
    }
}
