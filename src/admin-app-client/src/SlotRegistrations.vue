<script setup lang="ts">
import { computed, ref } from 'vue'
import * as DataTransfer from './DataTransfer'
import * as DateTime from './DateTime'
import * as Text from './Text'
import { uiFetchAuthorized } from './UIFetch'

const props = defineProps<{
  eventSlot: DataTransfer.Slot
  registrationFields: DataTransfer.RegistrationField[]
}>()

const getLinkUrl = (field: DataTransfer.RegistrationField, value: string | undefined) => {
  if (value === undefined || value === '') return undefined
  switch (field.type) {
    case 'mail': return `mailto:${value}`
    case 'phone': return `tel:${value}`
    case 'text': return undefined
  }
}

const summary = computed(() => {
  const activeRegistrations = props.eventSlot.registrations.filter(v => v.deregistrationTime === null)
  const registrations = activeRegistrations.filter(v => !v.isRequest)
  const requests = activeRegistrations.filter(v => v.isRequest)
  return {
    registrationCount: registrations.length,
    registeredQuantity: registrations.reduce((sum, v) => sum + v.quantity, 0),
    requestCount: requests.length,
    requestedQuantity: requests.reduce((sum, v) => sum + v.quantity, 0),
  }
})

const emit = defineEmits<{
  registrationCancelled: []
}>()

const registrationMarkedForCancellation = ref<string>() // url of registration
const isCancelling = ref(false)
const hasCancellingFailed = ref(false)
const cancelError = ref<string>()
const cancelRegistration = async (registration: DataTransfer.EventRegistration) => {
  if (isCancelling.value) return
  if (registrationMarkedForCancellation.value !== registration.url) {
    registrationMarkedForCancellation.value = registration.url
    return
  }

  cancelError.value = undefined
  const result = await uiFetchAuthorized(isCancelling, hasCancellingFailed, registration.url, { method: 'DELETE' })
  registrationMarkedForCancellation.value = undefined
  if (result.succeeded) {
    emit('registrationCancelled') // event list (incl. registrations, counts and remaining capacity) must be reloaded
  }
  else {
    cancelError.value = 'Fehler beim Stornieren der Registrierung.'
  }
}
</script>

<template>
  <div class="border rounded p-4 flex flex-col gap-2">
    <div class="flex flex-wrap items-baseline gap-x-4 gap-y-1">
      <span class="rounded bg-blue-htlvb/50 -mx-2 px-2">{{ DateTime.format(new Date(eventSlot.startTime)) }}</span>
      <span class="text-sm">{{ Text.pluralize(summary.registrationCount, 'Registrierung', 'Registrierungen') }} ({{ Text.pluralize(summary.registeredQuantity, 'Person', 'Personen') }})</span>
      <span v-if="summary.requestCount > 0" class="text-sm">{{ Text.pluralize(summary.requestCount, 'Anfrage', 'Anfragen') }} ({{ Text.pluralize(summary.requestedQuantity, 'Person', 'Personen') }})</span>
      <span class="text-sm">Freie Plätze: {{ eventSlot.remainingCapacity !== null ? eventSlot.remainingCapacity : "unbegrenzt" }}</span>
    </div>
    <span v-if="cancelError !== undefined" class="text-rose-400">{{ cancelError }}</span>
    <span v-if="eventSlot.registrations.length === 0" class="text-sm">Keine Registrierungen vorhanden.</span>
    <!-- scrolls horizontally on small screens instead of wrapping lines -->
    <div v-else class="overflow-x-auto">
      <table class="w-full text-sm text-left whitespace-nowrap">
        <thead>
          <tr class="border-b">
            <th v-for="field in registrationFields" :key="field.key" class="font-semibold px-2 py-1">{{ field.label }}</th>
            <th class="font-semibold px-2 py-1 text-right">Personen</th>
            <th class="font-semibold px-2 py-1">Art</th>
            <th class="font-semibold px-2 py-1">Registriert</th>
            <th class="font-semibold px-2 py-1">Storniert</th>
            <th class="px-2 py-1"><span class="sr-only">Aktionen</span></th>
          </tr>
        </thead>
        <tbody class="divide-y">
          <tr v-for="registration in eventSlot.registrations" :key="registration.url" :class="{ 'opacity-50': registration.deregistrationTime !== null }">
            <td v-for="field in registrationFields" :key="field.key" class="px-2 py-1" :class="{ 'line-through': registration.deregistrationTime !== null }">
              <a v-if="getLinkUrl(field, registration.data[field.key]) !== undefined" :href="getLinkUrl(field, registration.data[field.key])" class="underline">{{ registration.data[field.key] }}</a>
              <template v-else>{{ registration.data[field.key] }}</template>
            </td>
            <td class="px-2 py-1 text-right">{{ registration.quantity }}</td>
            <td class="px-2 py-1">
              <span v-if="registration.isRequest" class="rounded bg-amber-400/30 px-1">Anfrage</span>
              <span v-else>Registrierung</span>
            </td>
            <td class="px-2 py-1">{{ DateTime.format(new Date(registration.timestamp)) }}</td>
            <td class="px-2 py-1">{{ registration.deregistrationTime !== null ? DateTime.format(new Date(registration.deregistrationTime)) : '' }}</td>
            <td class="px-2 py-1 text-right">
              <a v-if="registration.deregistrationTime === null"
                class="p-2 cursor-pointer"
                :class="{ 'text-rose-400': registrationMarkedForCancellation === registration.url, 'opacity-50 cursor-wait!': isCancelling }"
                :title="registrationMarkedForCancellation === registration.url ? 'Wirklich stornieren' : 'Stornieren'"
                @click="cancelRegistration(registration)">
                <i class="fa-solid fa-user-xmark"></i>
              </a>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
