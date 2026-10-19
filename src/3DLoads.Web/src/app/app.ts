import { ChangeDetectorRef, Component, DoCheck } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ScenarioDto, SolutionResponse } from './models/api.models';
import { DraftScenario } from './models/draft-scenario.model';
import { createDemoScenarioDraft } from './models/demo-scenario';
import {
  createDraftPackage,
  createEmptyScenarioDraft,
  toScenarioDto,
} from './models/scenario-draft.factory';
import { validateScenarioDraft } from './models/scenario-draft.validation';
import { ApiService } from './services/api.service';
import { LeftPanelComponent } from './components/left-panel/left-panel.component';
import { VanViewerComponent } from './components/van-viewer/van-viewer.component';

@Component({
  imports: [LeftPanelComponent, VanViewerComponent],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App implements DoCheck {
  protected readonly orientationOptions = [
    { value: 1, label: 'Fixed' },
    { value: 2, label: 'This Side Up' },
    { value: 3, label: 'Free 90' },
  ];

  protected draftScenario = createEmptyScenarioDraft();
  protected viewerScenario: ScenarioDto = this.toScenarioDto();
  protected solution: SolutionResponse | null = null;
  protected busy = false;
  protected statusMessage = 'Ready';
  protected errorMessage = '';

  private lastDraftSignature = JSON.stringify(this.draftScenario);

  public constructor(
    private readonly api: ApiService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  public ngDoCheck(): void {
    const signature = JSON.stringify(this.draftScenario);
    if (signature === this.lastDraftSignature) return;
    this.lastDraftSignature = signature;
    this.solution = null;
    this.viewerScenario = this.toScenarioDto();
    this.statusMessage = 'Draft changed';
  }

  protected addPackage(): void {
    this.draftScenario.packages = [
      ...this.draftScenario.packages,
      createDraftPackage(this.draftScenario.packages.length + 1),
    ];
  }

  protected removePackage(index: number): void {
    this.draftScenario.packages = this.draftScenario.packages.filter((_, i) => i !== index);
  }

  protected resetDemo(): void {
    this.draftScenario = createEmptyScenarioDraft();
    this.solution = null;
    this.viewerScenario = this.toScenarioDto();
    this.lastDraftSignature = JSON.stringify(this.draftScenario);
    this.statusMessage = 'Fresh demo scenario';
    this.errorMessage = '';
  }

  protected loadDemo(): void {
    this.draftScenario = createDemoScenarioDraft();
    this.solution = null;
    this.viewerScenario = this.toScenarioDto();
    this.lastDraftSignature = JSON.stringify(this.draftScenario);
    this.statusMessage = 'Demo loaded. Ready to solve.';
    this.errorMessage = '';
  }

  protected async runSolve(): Promise<void> {
    this.errorMessage = validateScenarioDraft(this.draftScenario);
    if (this.errorMessage) return;

    this.busy = true;
    this.solution = null;
    this.viewerScenario = this.toScenarioDto();
    this.lastDraftSignature = JSON.stringify(this.draftScenario);
    this.statusMessage = 'Solving';

    try {
      this.solution = await firstValueFrom(this.api.runSolve(this.viewerScenario));
      this.statusMessage = 'Solve complete';
    } catch (error) {
      this.errorMessage = error instanceof Error ? error.message : 'The solve failed.';
      this.statusMessage = 'Solve failed';
    } finally {
      this.busy = false;
      this.changeDetector.markForCheck();
    }
  }

  private toScenarioDto(): ScenarioDto {
    return toScenarioDto(this.draftScenario);
  }
}
