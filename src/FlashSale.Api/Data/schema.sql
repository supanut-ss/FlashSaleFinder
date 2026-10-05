CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "Marketplace" character varying(20) NOT NULL,
    "ExternalProductId" character varying(100) NOT NULL,
    "ExternalVariantId" character varying(100) NOT NULL,
    "Title" character varying(300) NOT NULL,
    "VariantLabel" character varying(150) NOT NULL,
    "Category" character varying(100) NOT NULL,
    "ProductUrl" character varying(2048),
    "ImageUrl" character varying(2048),
    "ComparableVariantKey" character varying(150),
    "IsSample" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Products_Marketplace" CHECK ("Marketplace" IN ('Lazada', 'Shopee', 'TikTok'))
);


CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Issuer" character varying(300) NOT NULL,
    "Subject" character varying(200) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);


CREATE TABLE "PriceObservations" (
    "Id" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Price" numeric(12,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "CapturedAt" timestamp with time zone NOT NULL,
    "SourceObservationId" character varying(150) NOT NULL,
    "InStock" boolean NOT NULL,
    CONSTRAINT "PK_PriceObservations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Observations_Currency" CHECK ("Currency" = 'THB'),
    CONSTRAINT "CK_Observations_Price" CHECK ("Price" >= 0),
    CONSTRAINT "FK_PriceObservations_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);


CREATE TABLE "WatchlistEntries" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WatchlistEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WatchlistEntries_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_WatchlistEntries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PriceAlerts" (
    "Id" uuid NOT NULL,
    "WatchlistEntryId" uuid NOT NULL,
    "TargetPrice" numeric(12,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Enabled" boolean NOT NULL,
    "LastTriggeredAt" timestamp with time zone,
    CONSTRAINT "PK_PriceAlerts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Alerts_Currency" CHECK ("Currency" = 'THB'),
    CONSTRAINT "CK_Alerts_Price" CHECK ("TargetPrice" >= 0),
    CONSTRAINT "FK_PriceAlerts_WatchlistEntries_WatchlistEntryId" FOREIGN KEY ("WatchlistEntryId") REFERENCES "WatchlistEntries" ("Id") ON DELETE CASCADE
);


CREATE UNIQUE INDEX "IX_PriceAlerts_WatchlistEntryId" ON "PriceAlerts" ("WatchlistEntryId");


CREATE INDEX "IX_PriceObservations_ProductId_CapturedAt" ON "PriceObservations" ("ProductId", "CapturedAt");


CREATE UNIQUE INDEX "IX_PriceObservations_ProductId_SourceObservationId" ON "PriceObservations" ("ProductId", "SourceObservationId");


CREATE INDEX "IX_Products_Category" ON "Products" ("Category");


CREATE UNIQUE INDEX "IX_Products_Marketplace_ExternalProductId_ExternalVariantId" ON "Products" ("Marketplace", "ExternalProductId", "ExternalVariantId");


CREATE UNIQUE INDEX "IX_Users_Issuer_Subject" ON "Users" ("Issuer", "Subject");


CREATE INDEX "IX_WatchlistEntries_ProductId" ON "WatchlistEntries" ("ProductId");


CREATE UNIQUE INDEX "IX_WatchlistEntries_UserId_ProductId" ON "WatchlistEntries" ("UserId", "ProductId");


