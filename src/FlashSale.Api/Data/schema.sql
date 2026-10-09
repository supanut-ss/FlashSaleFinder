ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `Products` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Marketplace` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ExternalProductId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `ExternalVariantId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `Title` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `VariantLabel` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Category` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ProductUrl` varchar(2048) CHARACTER SET utf8mb4 NULL,
    `ImageUrl` varchar(2048) CHARACTER SET utf8mb4 NULL,
    `ComparableVariantKey` varchar(150) CHARACTER SET utf8mb4 NULL,
    `OriginalPrice` decimal(12,2) NULL,
    `AffiliateUrl` varchar(2048) CHARACTER SET utf8mb4 NULL,
    `DealEndAt` datetime(6) NULL,
    `IsSample` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Products` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Products_Marketplace` CHECK (Marketplace IN ('Lazada', 'Shopee', 'TikTok')),
    CONSTRAINT `CK_Products_OriginalPrice` CHECK (OriginalPrice IS NULL OR OriginalPrice >= 0)
) CHARACTER SET=utf8mb4;


CREATE TABLE `Users` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Issuer` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `Subject` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `PriceObservations` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProductId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Price` decimal(12,2) NOT NULL,
    `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
    `CapturedAt` datetime(6) NOT NULL,
    `SourceObservationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `InStock` tinyint(1) NOT NULL,
    CONSTRAINT `PK_PriceObservations` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Observations_Currency` CHECK (Currency = 'THB'),
    CONSTRAINT `CK_Observations_Price` CHECK (Price >= 0),
    CONSTRAINT `FK_PriceObservations_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `WatchlistEntries` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProductId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_WatchlistEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_WatchlistEntries_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WatchlistEntries_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `PriceAlerts` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `WatchlistEntryId` char(36) COLLATE ascii_general_ci NOT NULL,
    `TargetPrice` decimal(12,2) NOT NULL,
    `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `LastTriggeredAt` datetime(6) NULL,
    `LastObservedPrice` decimal(12,2) NULL,
    `LastEvaluatedObservationId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_PriceAlerts` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Alerts_Currency` CHECK (Currency = 'THB'),
    CONSTRAINT `CK_Alerts_Price` CHECK (TargetPrice >= 0),
    CONSTRAINT `FK_PriceAlerts_WatchlistEntries_WatchlistEntryId` FOREIGN KEY (`WatchlistEntryId`) REFERENCES `WatchlistEntries` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AlertEvents` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PriceAlertId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ObservationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Price` decimal(12,2) NOT NULL,
    `TargetPrice` decimal(12,2) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_AlertEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_AlertEvents_Price` CHECK (Price >= 0),
    CONSTRAINT `CK_AlertEvents_TargetPrice` CHECK (TargetPrice >= 0),
    CONSTRAINT `FK_AlertEvents_PriceAlerts_PriceAlertId` FOREIGN KEY (`PriceAlertId`) REFERENCES `PriceAlerts` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AlertEvents_PriceObservations_ObservationId` FOREIGN KEY (`ObservationId`) REFERENCES `PriceObservations` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AlertEvents_ObservationId` ON `AlertEvents` (`ObservationId`);


CREATE UNIQUE INDEX `IX_AlertEvents_PriceAlertId_ObservationId` ON `AlertEvents` (`PriceAlertId`, `ObservationId`);


CREATE UNIQUE INDEX `IX_PriceAlerts_WatchlistEntryId` ON `PriceAlerts` (`WatchlistEntryId`);


CREATE INDEX `IX_PriceObservations_ProductId_CapturedAt` ON `PriceObservations` (`ProductId`, `CapturedAt`);


CREATE UNIQUE INDEX `IX_PriceObservations_ProductId_SourceObservationId` ON `PriceObservations` (`ProductId`, `SourceObservationId`);


CREATE INDEX `IX_Products_Category` ON `Products` (`Category`);


CREATE UNIQUE INDEX `IX_Products_Marketplace_ExternalProductId_ExternalVariantId` ON `Products` (`Marketplace`, `ExternalProductId`, `ExternalVariantId`);


CREATE UNIQUE INDEX `IX_Users_Issuer_Subject` ON `Users` (`Issuer`, `Subject`);


CREATE INDEX `IX_WatchlistEntries_ProductId` ON `WatchlistEntries` (`ProductId`);


CREATE UNIQUE INDEX `IX_WatchlistEntries_UserId_ProductId` ON `WatchlistEntries` (`UserId`, `ProductId`);


