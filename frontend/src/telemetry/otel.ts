import { context, trace } from '@opentelemetry/api';
import { W3CTraceContextPropagator } from '@opentelemetry/core';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { Resource } from '@opentelemetry/resources';
import { SemanticResourceAttributes } from '@opentelemetry/semantic-conventions';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';

const tracingEnabled = import.meta.env.VITE_TRACING_ENABLED !== 'false';

if (tracingEnabled && typeof window !== 'undefined') {
  const configuredEndpoint = import.meta.env.VITE_OTEL_EXPORTER_OTLP_ENDPOINT;
  const isDev = import.meta.env.MODE === 'development' || import.meta.env.MODE === 'localdev';
  const isLocalHost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
  const exporterEndpoint = isDev || isLocalHost
    ? new URL('/v1/traces', window.location.origin).toString()
    : (configuredEndpoint && configuredEndpoint.length > 0
        ? configuredEndpoint
        : new URL('/v1/traces', window.location.origin).toString());

  const resource = new Resource({
    [SemanticResourceAttributes.SERVICE_NAME]: 'foundry-agent-frontend',
    [SemanticResourceAttributes.DEPLOYMENT_ENVIRONMENT]: import.meta.env.MODE,
  });

  const provider = new WebTracerProvider({ resource });
  provider.addSpanProcessor(new BatchSpanProcessor(
    new OTLPTraceExporter({ endpoint: exporterEndpoint })
  ));

  provider.register({
    contextManager: new ZoneContextManager(),
    propagator: new W3CTraceContextPropagator(),
  });

  registerInstrumentations({
    instrumentations: [
      new FetchInstrumentation({
        clearTimingResources: true,
        ignoreUrls: [/\/v1\/traces/],
        propagateTraceHeaderCorsUrls: [
          /^\/api\//,
          /http:\/\/localhost:8080\/api\//,
        ],
      }),
    ],
  });
}

export const appTracer = trace.getTracer('foundry-agent-frontend');
export const tracingContext = context;
