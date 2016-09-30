CREATE TABLE [dbo].[WebApplications] (
    [Id]    UNIQUEIDENTIFIER NOT NULL,
    [Title] NVARCHAR (MAX)   NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
  
CREATE TABLE [dbo].[SiteCollections] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [Url]              NVARCHAR (MAX)   NOT NULL,
    [WebApplicationId] UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SiteCollections_WebApplications] FOREIGN KEY ([WebApplicationId]) REFERENCES [dbo].[WebApplications] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[Webs] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [Title]            NVARCHAR (MAX)   NOT NULL,
    [Url]              NVARCHAR (MAX)   NOT NULL,
    [ParentWebId]      UNIQUEIDENTIFIER NULL,
    [SiteCollectionId] UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Webs_Webs] FOREIGN KEY ([ParentWebId]) REFERENCES [dbo].[Webs] ([Id]),
    CONSTRAINT [FK_Webs_SiteCollections] FOREIGN KEY ([SiteCollectionId]) REFERENCES [dbo].[SiteCollections] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[Lists] (
    [Id]    UNIQUEIDENTIFIER NOT NULL,
    [Title] NVARCHAR (MAX)   NOT NULL,
    [WebId] UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Lists_Webs] FOREIGN KEY ([WebId]) REFERENCES [dbo].[Webs] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[ListItems] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [ItemId]        INT              NOT NULL,
    [Title]         NVARCHAR (MAX)   NOT NULL,
    [Url]           NVARCHAR (MAX)   NOT NULL,
    [CreatedAt]     DATETIME         NOT NULL,
    [ModifiedAt]    DATETIME         NOT NULL,
    [ContentTypeId] NVARCHAR (MAX)   NOT NULL,
    [ListId]        UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ListItems_Lists] FOREIGN KEY ([ListId]) REFERENCES [dbo].[Lists] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[DocumentLibraries] (
    [Id]    UNIQUEIDENTIFIER NOT NULL,
    [Title] NVARCHAR (MAX)   NOT NULL,
    [WebId] UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_DocumentLibraries_Webs] FOREIGN KEY ([WebId]) REFERENCES [dbo].[Webs] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[DocumentLibraryItems] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [ItemId]            INT              NOT NULL,
    [Title]             NVARCHAR (MAX)   NOT NULL,
    [Url]               NVARCHAR (MAX)   NOT NULL,
    [CreatedAt]         DATETIME         NOT NULL,
    [ModifiedAt]        DATETIME         NOT NULL,
    [ContentTypeId]     NVARCHAR (MAX)   NOT NULL,
    [DocumentLibraryId] UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR (MAX)   NOT NULL,
    [Length]            INT              NOT NULL,
    [CheckOutStatus]    NVARCHAR (MAX)   NOT NULL,
    [CheckedOutBy]      NVARCHAR (MAX)   NULL, -- Only present when CheckOutStatus <> "None"
    [CheckedOutDate]    DATETIME         NULL, -- Only present when CheckOutStatus <> "None" 
    [MajorVersion]      INT              NOT NULL,
    [MinorVersion]      INT              NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC), 
    CONSTRAINT [FK_DocumentLibraryItems_DocumentLibraries] FOREIGN KEY ([DocumentLibraryId]) REFERENCES [DocumentLibraries]([Id]),
);


