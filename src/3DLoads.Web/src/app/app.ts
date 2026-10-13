import { CommonModule } from '@angular/common';
import { Component, DoCheck, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, interval, Subscription, switchMap, takeWhile, startWith } from 'rxjs';
import {
  CreateScenarioRequest,
  PackageRequest,
  ScenarioDto,
  ScenarioSummaryResponse,
  SolutionResponse,
  SolveJobResponse,
  VanRequest,
} from './models/api.models';
import { ApiService } from './services/api.service';
import { VanViewerComponent } from './components/van-viewer.component';

interface DraftPackage {
  externalReference: string;
  length: number;
  width: number;
  height: number;
  weight: number;
  deliveryStopIndex: number;
  orientationPolicy: number;
  maxLoadBearingWeight: number;
}

interface DraftScenario {
  name: string;
  van: VanRequest;
  packages: DraftPackage[];
}

@Component({
  imports: [CommonModule, FormsModule, VanViewerComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App implements DoCheck, OnDestroy {
  protected readonly title = signal('3DLoads Visualizer');
  protected readonly orientationOptions = [
    { value: 1, label: 'Fixed' },
    { value: 2, label: 'This Side Up' },
    { value: 3, label: 'Free 90' },
  ];

  protected apiBaseUrl = 'https://localhost:7273';
  protected username = 'admin';
  protected password = 'admin';

  protected token = '';
  protected scenarios: ScenarioSummaryResponse[] = [];
  protected selectedScenarioId = '';
  protected phases = 'InitialGreedy,Brkga,Cleanup,FinalValidation';
  protected randomSeed = 42;
  protected timeBudgetSeconds = 5;
  protected editingScenarioId = '';
  protected draftScenario: DraftScenario = this.createEmptyScenarioDraft();

  protected activeJob: SolveJobResponse | null = null;
  protected solution: SolutionResponse | null = null;
  protected scenario: ScenarioDto | null = null;
  protected viewerScenario: ScenarioDto | null = null;

  protected busy = false;
  protected statusMessage = 'Connect to API and run a solve to visualize the van load.';
  protected errorMessage = '';

  private pollSubscription: Subscription | null = null;
  private lastDraftSignature = '';

  public constructor(private readonly api: ApiService) {}

  protected get isEditingScenario(): boolean {
    return this.editingScenarioId.length > 0;
  }

  public ngDoCheck(): void {
    const nextSignature = this.buildDraftSignature();
    if (nextSignature === this.lastDraftSignature) {
      return;
    }

    this.lastDraftSignature = nextSignature;

    if (!this.solution) {
      this.viewerScenario = this.buildViewerScenarioFromDraft();
    }
  }

  public ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  protected async signIn(): Promise<void> {
    this.resetMessages();
    this.busy = true;

    try {
      const tokenResponse = await firstValueFrom(
        this.api.createToken(this.apiBaseUrl, this.username.trim(), this.password),
      );
      this.token = tokenResponse.token;
      this.syncViewerScenario();
      this.statusMessage = 'Authenticated. Load scenarios to pick one.';
    } catch (error) {
      this.errorMessage = this.toErrorMessage(error, 'Authentication failed.');
    } finally {
      this.busy = false;
    }
  }

  protected async loadScenarios(): Promise<void> {
    this.resetMessages();
    if (!this.ensureToken()) {
      return;
    }

    this.busy = true;
    try {
      this.scenarios = await firstValueFrom(this.api.listScenarios(this.apiBaseUrl, this.token));
      if (this.scenarios.length > 0 && !this.selectedScenarioId) {
        this.selectedScenarioId = this.scenarios[0].id;
      }
      this.syncViewerScenario();
      this.statusMessage = `Loaded ${this.scenarios.length} scenario(s).`;
    } catch (error) {
      this.errorMessage = this.toErrorMessage(error, 'Could not load scenarios.');
    } finally {
      this.busy = false;
    }
  }

  protected newScenarioDraft(): void {
    this.editingScenarioId = '';
    this.solution = null;
    this.scenario = null;
    this.activeJob = null;
    this.draftScenario = this.createEmptyScenarioDraft();
    this.syncViewerScenario();
    this.statusMessage = 'Draft reset. Enter van and package data to create a scenario.';
    this.errorMessage = '';
  }

  protected addDraftPackage(): void {
    this.draftScenario = {
      ...this.draftScenario,
      packages: [...this.draftScenario.packages, this.createEmptyPackageDraft()],
    };
    this.syncViewerScenario();
  }

  protected removeDraftPackage(index: number): void {
    this.draftScenario = {
      ...this.draftScenario,
      packages: this.draftScenario.packages.filter((_, currentIndex) => currentIndex !== index),
    };
    this.syncViewerScenario();
  }

  protected async loadSelectedScenarioIntoEditor(): Promise<void> {
    this.resetMessages();
    if (!this.ensureToken() || !this.selectedScenarioId) {
      return;
    }

    this.busy = true;
    try {
      const scenario = await firstValueFrom(
        this.api.getScenario(this.apiBaseUrl, this.token, this.selectedScenarioId),
      );

      this.scenario = scenario;
      this.solution = null;
      this.activeJob = null;
      this.editingScenarioId = scenario.id;
      this.draftScenario = {
        name: scenario.name,
        van: {
          name: scenario.van.name,
          length: scenario.van.length,
          width: scenario.van.width,
          height: scenario.van.height,
          maxPayloadWeight: scenario.van.maxPayloadWeight,
        },
        packages: scenario.packages.map((pkg) => ({
          externalReference: pkg.externalReference,
          length: pkg.length,
          width: pkg.width,
          height: pkg.height,
          weight: pkg.weight,
          deliveryStopIndex: pkg.deliveryStopIndex,
          orientationPolicy: pkg.orientationPolicy,
          maxLoadBearingWeight: pkg.maxLoadBearingWeight,
        })),
      };
      this.syncViewerScenario();
      this.statusMessage = `Loaded ${scenario.name} into the editor.`;
    } catch (error) {
      this.errorMessage = this.toErrorMessage(error, 'Could not load the selected scenario.');
    } finally {
      this.busy = false;
    }
  }

  protected async saveScenarioDraft(): Promise<void> {
    this.resetMessages();
    if (!this.ensureToken()) {
      return;
    }

    const validationError = this.validateDraftScenario();
    if (validationError) {
      this.errorMessage = validationError;
      return;
    }

    this.busy = true;
    this.solution = null;
    this.activeJob = null;

    try {
      const request = this.toScenarioRequest(this.draftScenario);
      const summary = this.isEditingScenario
        ? await firstValueFrom(
            this.api.updateScenario(this.apiBaseUrl, this.token, this.editingScenarioId, request),
          )
        : await firstValueFrom(this.api.createScenario(this.apiBaseUrl, this.token, request));

      this.selectedScenarioId = summary.id;
      this.editingScenarioId = summary.id;
      await this.loadScenarios();

      this.scenario = await firstValueFrom(this.api.getScenario(this.apiBaseUrl, this.token, summary.id));
      this.draftScenario = {
        name: this.scenario.name,
        van: {
          name: this.scenario.van.name,
          length: this.scenario.van.length,
          width: this.scenario.van.width,
          height: this.scenario.van.height,
          maxPayloadWeight: this.scenario.van.maxPayloadWeight,
        },
        packages: this.scenario.packages.map((pkg) => ({
          externalReference: pkg.externalReference,
          length: pkg.length,
          width: pkg.width,
          height: pkg.height,
          weight: pkg.weight,
          deliveryStopIndex: pkg.deliveryStopIndex,
          orientationPolicy: pkg.orientationPolicy,
          maxLoadBearingWeight: pkg.maxLoadBearingWeight,
        })),
      };
      this.syncViewerScenario();

      this.statusMessage = this.isEditingScenario
        ? `Scenario updated: ${summary.name}`
        : `Scenario created: ${summary.name}`;
    } catch (error) {
      this.errorMessage = this.toErrorMessage(error, 'Could not save scenario.');
    } finally {
      this.busy = false;
    }
  }

  protected async runSolve(): Promise<void> {
    this.resetMessages();

    if (!this.ensureToken()) {
      return;
    }

    if (!this.selectedScenarioId) {
      this.errorMessage = 'Pick a scenario before starting a solve.';
      return;
    }

    const phaseNames = this.phases
      .split(',')
      .map((x) => x.trim())
      .filter((x) => x.length > 0);

    this.busy = true;
    this.solution = null;
    this.activeJob = null;
    this.syncViewerScenario();

    try {
      const solveJob = await firstValueFrom(
        this.api.startSolve(this.apiBaseUrl, this.token, this.selectedScenarioId, {
          phases: phaseNames,
          randomSeed: this.randomSeed,
          timeBudgetSeconds: this.timeBudgetSeconds,
        }),
      );

      this.activeJob = solveJob;
      this.statusMessage = `Solve queued: ${solveJob.id}`;
      this.startPolling(solveJob.id);
    } catch (error) {
      this.errorMessage = this.toErrorMessage(error, 'Failed to start solve.');
    } finally {
      this.busy = false;
    }
  }

  private startPolling(jobId: string): void {
    this.pollSubscription?.unsubscribe();

    this.pollSubscription = interval(750)
      .pipe(
        startWith(0),
        switchMap(() => this.api.getSolveJob(this.apiBaseUrl, this.token, jobId)),
        takeWhile((job) => job.status === 1 || job.status === 2, true),
      )
      .subscribe({
        next: (job) => {
          void this.onJobStatus(job).catch((error: unknown) => {
            this.errorMessage = this.toErrorMessage(error, 'Loading the completed solution failed.');
            this.statusMessage = 'Solve completed, but the result could not be loaded.';
          });
        },
        error: (error) => {
          this.errorMessage = this.toErrorMessage(error, 'Polling solve job failed.');
        },
      });
  }

  private async onJobStatus(job: SolveJobResponse): Promise<void> {
    this.activeJob = job;

    if (job.status === 1) {
      this.statusMessage = 'Solve job queued...';
      return;
    }

    if (job.status === 2) {
      this.statusMessage = 'Solve job running...';
      return;
    }

    if (job.status === 4) {
      this.errorMessage = job.errorMessage || 'Solve job failed.';
      this.statusMessage = 'Solve failed.';
      return;
    }

    if (job.status === 3) {
      this.statusMessage = 'Solve completed. Loading scenario and solution...';
      await this.loadCompletedSolution(job);
    }
  }

  private ensureToken(): boolean {
    if (!this.token) {
      this.errorMessage = 'Authenticate first.';
      return false;
    }

    return true;
  }

  private resetMessages(): void {
    this.errorMessage = '';
  }

  private async loadCompletedSolution(job: SolveJobResponse): Promise<void> {
    let lastError: unknown = null;

    for (let attempt = 0; attempt < 5; attempt++) {
      try {
        const [scenario, solution] = await Promise.all([
          firstValueFrom(this.api.getScenario(this.apiBaseUrl, this.token, job.scenarioId)),
          firstValueFrom(this.api.getSolveResult(this.apiBaseUrl, this.token, job.id)),
        ]);

        if (!solution.id || !Array.isArray(solution.placements)) {
          throw new Error('Solve result is not ready yet.');
        }

        this.scenario = scenario;
        this.editingScenarioId = scenario.id;
        this.draftScenario = this.toDraftScenario(scenario);
        this.solution = solution;
        this.syncViewerScenario();
        this.statusMessage = `Loaded solution ${solution.id}`;
        return;
      } catch (error) {
        lastError = error;
        await this.delay(500);
      }
    }

    throw lastError ?? new Error('Loading the completed solution failed.');
  }

  private createEmptyScenarioDraft(): DraftScenario {
    return {
      name: 'Local delivery run',
      van: {
        name: 'Transit LWB',
        length: 10,
        width: 4,
        height: 4,
        maxPayloadWeight: 1200,
      },
      packages: [this.createEmptyPackageDraft()],
    };
  }

  private toDraftScenario(scenario: ScenarioDto): DraftScenario {
    return {
      name: scenario.name,
      van: {
        name: scenario.van.name,
        length: scenario.van.length,
        width: scenario.van.width,
        height: scenario.van.height,
        maxPayloadWeight: scenario.van.maxPayloadWeight,
      },
      packages: scenario.packages.map((pkg) => ({
        externalReference: pkg.externalReference,
        length: pkg.length,
        width: pkg.width,
        height: pkg.height,
        weight: pkg.weight,
        deliveryStopIndex: pkg.deliveryStopIndex,
        orientationPolicy: pkg.orientationPolicy,
        maxLoadBearingWeight: pkg.maxLoadBearingWeight,
      })),
    };
  }

  private syncViewerScenario(): void {
    this.viewerScenario = this.solution && this.scenario
      ? this.scenario
      : this.buildViewerScenarioFromDraft();
    this.lastDraftSignature = this.buildDraftSignature();
  }

  private buildViewerScenarioFromDraft(): ScenarioDto | null {
    if (!this.draftScenario.name && this.draftScenario.packages.length === 0) {
      return this.scenario;
    }

    return {
      id: this.editingScenarioId || 'draft-scenario',
      name: this.draftScenario.name || 'Draft scenario',
      vanId: this.editingScenarioId ? `${this.editingScenarioId}-van` : 'draft-van',
      van: {
        ...this.draftScenario.van,
        id: this.editingScenarioId ? `${this.editingScenarioId}-van` : 'draft-van',
      },
      packages: this.draftScenario.packages.map((pkg, index) => ({
        id: `${this.editingScenarioId || 'draft'}-${index}`,
        externalReference: pkg.externalReference,
        length: pkg.length,
        width: pkg.width,
        height: pkg.height,
        weight: pkg.weight,
        deliveryStopIndex: pkg.deliveryStopIndex,
        orientationPolicy: pkg.orientationPolicy,
        maxLoadBearingWeight: pkg.maxLoadBearingWeight,
      })),
      createdAtUtc: new Date().toISOString(),
    };
  }

  private buildDraftSignature(): string {
    return JSON.stringify(this.draftScenario);
  }

  private async delay(milliseconds: number): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, milliseconds));
  }

  private createEmptyPackageDraft(): DraftPackage {
    const nextIndex = (this.draftScenario?.packages.length ?? 0) + 1;

    return {
	  externalReference: `box-${nextIndex}`,
      length: 2,
      width: 2,
      height: 2,
      weight: 10,
      deliveryStopIndex: 1,
      orientationPolicy: 3,
      maxLoadBearingWeight: 100,
    };
  }

  private validateDraftScenario(): string {
    if (!this.draftScenario.name.trim()) {
      return 'Scenario name is required.';
    }

    if (!this.draftScenario.van.name.trim()) {
      return 'Van name is required.';
    }

    if (this.draftScenario.packages.length === 0) {
      return 'At least one package is required.';
    }

    const invalidPackage = this.draftScenario.packages.find(
      (pkg) => !pkg.externalReference.trim() || pkg.length <= 0 || pkg.width <= 0 || pkg.height <= 0,
    );

    if (invalidPackage) {
      return 'Each package needs a label and positive dimensions.';
    }

    return '';
  }

  private toScenarioRequest(draft: DraftScenario): CreateScenarioRequest {
    return {
      name: draft.name.trim(),
      van: {
        name: draft.van.name.trim(),
        length: draft.van.length,
        width: draft.van.width,
        height: draft.van.height,
        maxPayloadWeight: draft.van.maxPayloadWeight,
      },
      packages: draft.packages.map((pkg) => this.toPackageRequest(pkg)),
    };
  }

  private toPackageRequest(pkg: DraftPackage): PackageRequest {
    return {
      externalReference: pkg.externalReference.trim(),
      length: pkg.length,
      width: pkg.width,
      height: pkg.height,
      weight: pkg.weight,
      deliveryStopIndex: pkg.deliveryStopIndex,
      orientationPolicy: pkg.orientationPolicy,
      maxLoadBearingWeight: pkg.maxLoadBearingWeight,
    };
  }

  private toErrorMessage(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'message' in error) {
      const message = (error as { message?: string }).message;
      if (message) {
        return message;
      }
    }

    return fallback;
  }
}
