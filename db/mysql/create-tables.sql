-- mysql

DROP TABLE
    `FlatEarthersAltDb`.`Users`,
    `FlatEarthersAltDb`.`Targets`,
    `FlatEarthersAltDb`.`UsersTargets`;

CREATE TABLE Users (
    UserGuid CHAR(36) PRIMARY KEY,
    FirstName VARCHAR(255),
    LastName VARCHAR(255),
    Email VARCHAR(255) UNIQUE NOT NULL,
    PasswordHash VARCHAR(255),
    EmailEnabled TINYINT(1) NOT NULL DEFAULT '1',
    IsAdmin TINYINT(1) NOT NULL DEFAULT '0'
);

CREATE TABLE Targets (
    TargetGuid CHAR(36) PRIMARY KEY,
    ScenePath INT NOT NULL,
    SceneRow INT NOT NULL,
    Latitude DECIMAL(10, 7) NOT NULL,
    Longitude DECIMAL(10, 7) NOT NULL,
    MinCloudCover DECIMAL(7, 5) NULL,
    MaxCloudCover DECIMAL(7,5) NULL,
    NotificationOffset DATETIME NOT NULL
);

CREATE TABLE UsersTargets (
    UserGuid CHAR(36),
    TargetGuid CHAR(36),
    CONSTRAINT FK_UsersTargets_Users FOREIGN KEY (UserGuid) REFERENCES Users(UserGuid),
    CONSTRAINT FK_UsersTargets_Targets FOREIGN KEY (TargetGuid) REFERENCES Targets(TargetGuid),
    PRIMARY KEY (UserGuid, TargetGuid)
);