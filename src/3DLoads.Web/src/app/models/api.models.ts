export interface TokenResponse {
  token: string;
  expiresAtUtc?: string;
}

export interface ScenarioSummaryResponse {
  id: string;
  name: string;
  vanName: string;
  packageCount: number;
  createdAtUtc: string;
}

export interface VanDto {
  id: string;
  name: string;
  length: number;
  width: number;
  height: number;
  maxPayloadWeight: number;
}

export interface PackageItemDto {
  id: string;
  externalReference: string;
  length: number;
  width: number;
  height: number;
  weight: number;
  deliveryStopIndex: number;
  orientationPolicy: number;
  maxLoadBearingWeight: number;
}

export interface ScenarioDto {
  id: string;
  name: string;
  vanId: string;
  van: VanDto;
  packages: PackageItemDto[];
  createdAtUtc: string;
}

export interface SolveJobResponse {
  id: string;
  scenarioId: string;
  status: number;
  solutionId?: string;
  errorMessage?: string;
  createdAtUtc: string;
  startedAtUtc?: string;
  completedAtUtc?: string;
}

export interface PlacementResponse {
  packageItemId: string;
  x: number;
  y: number;
  z: number;
  orientedLength: number;
  orientedWidth: number;
  orientedHeight: number;
}

export interface SolutionResponse {
  id: string;
  scenarioId: string;
  volumeUtilizationPercent: number;
  unpackedItemCount: number;
  unpackedVolume: number;
  isValid: boolean;
  validationErrors: string[];
  placements: PlacementResponse[];
}

export interface SolveScenarioRequest {
  phases?: string[];
  randomSeed?: number;
  timeBudgetSeconds: number;
}

export interface VanRequest {
  name: string;
  length: number;
  width: number;
  height: number;
  maxPayloadWeight: number;
}

export interface PackageRequest {
  externalReference: string;
  length: number;
  width: number;
  height: number;
  weight: number;
  deliveryStopIndex: number;
  orientationPolicy: number;
  maxLoadBearingWeight: number;
}

export interface CreateScenarioRequest {
  name: string;
  van: VanRequest;
  packages: PackageRequest[];
}
