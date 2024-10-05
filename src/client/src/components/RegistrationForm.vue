<script setup lang="tsx">
import type { BookingError, ReleasedSchedule, ReservationType, ScheduleEntry } from '@/DataTransfer'
import { uiFetch } from '@/UIFetch'
import { DateTime } from '@/Utils'
import _ from 'lodash'
import { marked } from 'marked'
import { computed, ref, watch } from 'vue'
import LoadButton from './LoadButton.vue'

const props = defineProps<{
  schedule: ReleasedSchedule
}>()

const datesWithFreeSlots = computed(() =>
  _(props.schedule.entries)
    .groupBy(v => new Date(v.startTime).toDateString())
    .map((v, k) => ({ date: k, freeSlots: getFreeSlots(v) }))
    .value()
)

const getFreeSlots = (entries: ScheduleEntry[]) => {
  return entries
    .map(v => {
      if (v.reservationType.type === 'free') return v.reservationType.remainingCapacity
      else return 0
    })
    .reduce((previous, current) => {
      if (current === null || previous === undefined) return undefined
      return previous + current
    }, 0 as number | undefined)
}

const totalFreeSlots = computed(() => getFreeSlots(props.schedule.entries))

const infoText = computed(() => marked.parse(props.schedule.infoText))

const selectedDate = ref<string>()
const selectedEntry = ref<ScheduleEntry>()
const selectedQuantity = ref<number>()
const contactName = ref<string>()
const contactMailAddress = ref<string>()
const contactPhoneNumber = ref<string>()

watch(selectedDate, () => {
  if (selectedEntry.value === undefined) return
  if (selectedDate.value === undefined) {
    selectedEntry.value = undefined
    return
  }
  const selectedDateTime = new Date(selectedDate.value)
  const selectedEntryTime = new Date(selectedEntry.value.startTime)

  selectedEntry.value = props.schedule.entries
    .find(v =>
      DateTime.dateEquals(selectedDateTime, new Date(v.startTime))
      && DateTime.timeEquals(selectedEntryTime, new Date(v.startTime))
    )
})

const selectedEntryFreeSlots = computed(() =>
{
  if (selectedEntry.value === undefined) return 0
  const reservationType = selectedEntry.value.reservationType
  if (reservationType.type === 'taken') return 0
  if (reservationType.remainingCapacity === null) return reservationType.maxQuantityPerBooking || 15
  if (reservationType.maxQuantityPerBooking === null) return reservationType.remainingCapacity || 15
  return Math.min(reservationType.maxQuantityPerBooking, reservationType.remainingCapacity)
})

watch(selectedEntryFreeSlots, freeSlots => {
  if (freeSlots === 1) {
    selectedQuantity.value = 1
  }
  else if (selectedQuantity.value !== undefined && selectedQuantity.value > freeSlots) {
    selectedQuantity.value = undefined
  }
})

const pluralize = (v: number, singularText: string, pluralText: string) => {
  if (v === 1) {
    return `${v} ${singularText}`
  }
  return `${v} ${pluralText}`
}

const isRegistering = ref(false)
const hasRegisteringFailed = ref(false)
const isRegistered = ref(false)
const registrationErrorMessages = ref([] as string[])
const doRegister = async () => {
  isRegistered.value = false
  if (selectedEntry.value === undefined) {
    registrationErrorMessages.value = [ 'Bitte wählen Sie Datum und Uhrzeit aus.' ]
    return
  }
  if (selectedEntry.value?.reservationType.type !== 'free') {
    registrationErrorMessages.value = [ `Zum ausgewählten Zeitpunkt sind leider keine Plätze mehr frei. Bitte kontaktieren Sie uns unter <a href="mailto:office@htlvb.at?subject=${props.schedule.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
    return
  }
  if (selectedQuantity.value === undefined) {
    registrationErrorMessages.value = [ `Bitte geben Sie die Anzahl der Personen an.` ]
    return
  }

  registrationErrorMessages.value = []

  const result = await uiFetch(isRegistering, hasRegisteringFailed, selectedEntry.value.reservationType.url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      quantity: selectedQuantity.value,
      name: contactName.value,
      mailAddress: contactMailAddress.value,
      phoneNumber: contactPhoneNumber.value,
    })
  })
  if (result.succeeded) {
    isRegistered.value = true
    selectedQuantity.value = undefined
    contactName.value = undefined
    contactMailAddress.value = undefined
    contactPhoneNumber.value = undefined
    selectedEntry.value.reservationType = await result.response.json() as ReservationType
    if (selectedEntry.value.reservationType.type === 'taken') {
      selectedEntry.value = undefined
    }
  }
  else if (result.response !== undefined) {
    let errors = await result.response.json() as BookingError[]
    for (const error of errors) {
      if (error.error === 'capacity-exceeded') {
        selectedEntry.value.reservationType = error.reservationType
        registrationErrorMessages.value.push(`Die Reservierung konnte nicht gespeichert werden, weil nicht mehr genügend Plätze frei sind.`)
      }
    }
  }
  else {
    registrationErrorMessages.value = [ `Beim Speichern der Registrierung ist ein Fehler aufgetreten. Bitte versuchen sie es erneut oder kontaktieren sie uns unter <a href="mailto:office@htlvb.at?subject=${props.schedule.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
  }
}
</script>

<template>
  <div v-if="totalFreeSlots === 0 && !isRegistered && !isRegistering && registrationErrorMessages.length === 0" class="flex justify-center items-center gap-2 p-4 rounded text-white font-semibold">
    <span>
      Leider sind keine Termine mehr frei.
      Bitte kontaktieren sie uns unter
      <a :href="`mailto:office@htlvb.at?subject=${props.schedule.title}`" class="underline">office@htlvb.at</a> bzw.
      <a href="tel:+43767224605" class="underline">07672/24605</a>.
    </span>
  </div>
  <template v-else>
    <div v-html="infoText" class="my-6"></div>
    <form @submit.prevent="doRegister">
      <fieldset :disabled="isRegistering">
        <div v-if="datesWithFreeSlots.length > 1">
          <h2 class="text-lg">Datum</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <template v-for="dateWithFreeSlots in datesWithFreeSlots" :key="dateWithFreeSlots">
              <button v-if="dateWithFreeSlots.freeSlots === undefined" type="button" class="!flex flex-col items-center justify-center button"
                :class="{ 'button-htlvb-selected': selectedDate === dateWithFreeSlots.date }" @click="selectedDate = dateWithFreeSlots.date">
                <span>{{ DateTime.formatDate(new Date(dateWithFreeSlots.date), { weekday: 'short' }) }}</span>
              </button>
              <button v-else-if="dateWithFreeSlots.freeSlots > 0" type="button" class="!flex flex-col items-center button"
                :class="{ 'button-htlvb-selected': selectedDate === dateWithFreeSlots.date }" @click="selectedDate = dateWithFreeSlots.date">
                <span>{{ DateTime.formatDate(new Date(dateWithFreeSlots.date), { weekday: 'short' }) }}</span>
                <span class="text-sm">{{ pluralize(dateWithFreeSlots.freeSlots, 'freier Platz', 'freie Plätze') }}</span>
              </button>
              <button v-else type="button" :disabled="true" class="!flex flex-col items-center button">
                <span>{{ DateTime.formatDate(new Date(dateWithFreeSlots.date), { weekday: 'short' }) }}</span>
                <span class="text-sm">ausgebucht</span>
              </button>
            </template>
          </div>
        </div>
        <template v-if="selectedDate !== undefined">
          <h2 class="text-lg mt-4">Uhrzeit</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <template v-for="entry in schedule.entries.filter(v => selectedDate !== undefined && DateTime.dateEquals(new Date(selectedDate), new Date(v.startTime)))"
              :key="entry.startTime">
              <template v-if="entry.reservationType.type === 'free'">
                <button v-if="entry.reservationType.remainingCapacity === null" type="button" class="!flex flex-col items-center justify-center button"
                  :class="{ 'button-htlvb-selected': selectedEntry === entry }" @click="selectedEntry = entry">
                  <span>{{ DateTime.formatTime(new Date(entry.startTime)) }}</span>
                </button>
                <button v-else type="button" class="!flex flex-col items-center button"
                  :class="{ 'button-htlvb-selected': selectedEntry === entry }" @click="selectedEntry = entry">
                  <span>{{ DateTime.formatTime(new Date(entry.startTime)) }}</span>
                  <span class="text-sm">{{ pluralize(entry.reservationType.remainingCapacity, 'freier Platz', 'freie Plätze') }}</span>
                </button>
              </template>
              <button v-else-if="entry.reservationType.type === 'taken'" type="button" :disabled="true" class="!flex flex-col items-center button">
                <span>{{ DateTime.formatTime(new Date(entry.startTime)) }}</span>
                <span class="text-sm">ausgebucht</span>
              </button>
            </template>
          </div>
        </template>
        <template v-if="selectedEntryFreeSlots > 1">
          <h2 class="text-lg mt-4">Anzahl Personen</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <button v-for="quantity in _.range(1, selectedEntryFreeSlots + 1)" :key="quantity" type="button" class="button"
              :class="{ 'button-htlvb-selected': selectedQuantity !== undefined && selectedQuantity >= quantity }"
              @click="selectedQuantity = quantity">
              {{ quantity }}
            </button>
          </div>
        </template>
        <h2 class="text-lg mt-4">Kontaktdaten</h2>
        <label class="input mt-2">
          <span class="input-label">Name</span>
          <input type="text" v-model="contactName" required class="input-text">
        </label>
        <label class="input mt-2">
          <span class="input-label">E-Mail-Adresse</span>
          <input type="email" v-model="contactMailAddress" required class="input-text">
        </label>
        <label class="input mt-2">
          <span class="input-label">Telefonnummer</span>
          <input type="tel" v-model="contactPhoneNumber" required class="input-text">
        </label>
        <LoadButton :loading="isRegistering" class="mt-2">Registrieren</LoadButton>
        <div class="mt-2">
          <span v-if="isRegistered" class="text-green-500">
            Ihre Registrierung wurde erfolgreich gespeichert. Sie erhalten in Kürze eine Bestätigung per Mail.
          </span>
          <ul v-else-if="registrationErrorMessages.length > 0" class="text-red-500">
            <li v-for="content in registrationErrorMessages" :key="content" v-html="content"></li>
          </ul>
        </div>
      </fieldset>
    </form>
  </template>
</template>