import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { createDemoScenarioDraft } from './models/demo-scenario';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient()],
    })
      .overrideComponent(App, {
        set: {
          template:
            '<button (click)="loadDemo()">Load Demo</button><p>{{ draftScenario.packages.length }}</p>',
        },
      })
      .compileComponents();
  });

  it('loads fifty demo packages across five stops without mounting the WebGL viewer', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('button')?.click();
    fixture.detectChanges();

    expect(compiled.querySelector('p')?.textContent).toBe('50');
  });

  it('assigns demo packages across all five delivery stops', () => {
    const scenario = createDemoScenarioDraft();
    expect(scenario.packages).toHaveLength(50);
    expect(new Set(scenario.packages.map((pkg) => pkg.deliveryStopIndex))).toEqual(
      new Set([1, 2, 3, 4, 5]),
    );
  });
});
