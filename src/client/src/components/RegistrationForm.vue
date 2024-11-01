<script setup lang="tsx">
import type { BookingError, BookingResult, ReleasedEvent, Slot } from '@/DataTransfer'
import { uiFetch } from '@/UIFetch'
import _ from 'lodash'
import { marked } from 'marked'
import { computed, ref, watch } from 'vue'
import LoadButton from './LoadButton.vue'
import SlotSelection from './SlotSelection.vue'

const props = defineProps<{
  event: ReleasedEvent
}>()

const hasFreeSlot = computed(() => {
  return props.event.slots.some(slot => {
    if (slot.type.type === 'free') {
      return slot.type.remainingCapacity === null || slot.type.remainingCapacity > 0
    }
    return false
  })
})

const infoText = computed(() => marked.parse(props.event.infoText))

const selectedSlot = ref<Slot>()
const selectedQuantity = ref<number>()
const contactName = ref<string>()
const contactMailAddress = ref<string>()
const contactPhoneNumber = ref<string>()

const selectedSlotMaxQuantity = computed(() =>
{
  if (selectedSlot.value === undefined) return 0
  const slotType = selectedSlot.value.type
  if (slotType.type === 'taken' || slotType.type === 'closed') return 0
  if (slotType.remainingCapacity === null) return slotType.maxQuantityPerBooking || 15
  if (slotType.maxQuantityPerBooking === null) return slotType.remainingCapacity || 15
  return Math.min(slotType.maxQuantityPerBooking, slotType.remainingCapacity)
})

watch(selectedSlotMaxQuantity, freeSlots => {
  if (freeSlots === 1) {
    selectedQuantity.value = 1
  }
  else if (selectedQuantity.value !== undefined && selectedQuantity.value > freeSlots) {
    selectedQuantity.value = undefined
  }
})

const isRegistering = ref(false)
const hasRegisteringFailed = ref(false)
const isRegistered = ref(false)
const isConfirmationMailSent = ref<boolean>()
const registrationErrorMessages = ref([] as string[])
const doRegister = async () => {
  isRegistered.value = false
  isConfirmationMailSent.value = undefined
  if (selectedSlot.value === undefined) {
    registrationErrorMessages.value = [ 'Bitte wählen Sie Datum und Uhrzeit aus.' ]
    return
  }
  if (selectedSlot.value?.type.type !== 'free') {
    registrationErrorMessages.value = [ `Zum ausgewählten Zeitpunkt sind leider keine Plätze mehr frei. Bitte kontaktieren Sie uns unter <a href="mailto:office@htlvb.at?subject=${props.event.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
    return
  }
  if (selectedQuantity.value === undefined) {
    registrationErrorMessages.value = [ `Bitte geben Sie die Anzahl der Personen an.` ]
    return
  }

  registrationErrorMessages.value = []

  const result = await uiFetch(isRegistering, hasRegisteringFailed, selectedSlot.value.type.url, {
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
    if (selectedSlotMaxQuantity.value > 1) {
      selectedQuantity.value = undefined
    }
    contactName.value = undefined
    contactMailAddress.value = undefined
    contactPhoneNumber.value = undefined
    const bookingResult = await result.response.json() as BookingResult
    selectedSlot.value.type = bookingResult.slotType
    isConfirmationMailSent.value = !bookingResult.mailSendError
    if (selectedSlot.value.type.type !== 'free') {
      selectedSlot.value = undefined
    }
  }
  else if (result.response !== undefined) {
    let errors = await result.response.json() as BookingError[]
    for (const error of errors) {
      switch (error.error)
      {
        case 'event-not-found':
          registrationErrorMessages.value.push('Das Event wurde nicht gefunden.')
          break
        case 'event-not-released':
          registrationErrorMessages.value.push('Die Reservierung ist noch nicht geöffnet.')
          break
        case 'slot-not-found':
          registrationErrorMessages.value.push('Der Slot wurde nicht gefunden.')
          break
        case 'slot-not-free':
          selectedSlot.value.type = error.slotType
          registrationErrorMessages.value.push('Der Slot kann nicht reserviert werden.')
          break
        case 'invalid-subscription-quantity':
          registrationErrorMessages.value.push('Bitte wählen Sie eine Anzahl aus.')
          break
        case 'invalid-subscriber-name':
          registrationErrorMessages.value.push('Bitte geben Sie Ihren Namen ein.')
          break
        case 'invalid-mail-address':
          registrationErrorMessages.value.push('Bitte geben Sie Ihre E-Mail-Adresse ein.')
          break
        case 'invalid-phone-number':
          registrationErrorMessages.value.push('Bitte geben Sie Ihre Telefonnummer ein.')
          break
        case 'max-quantity-per-booking-exceeded':
          registrationErrorMessages.value.push('Bitte wählen Sie eine Anzahl aus.')
          break
        case 'capacity-exceeded':
          selectedSlot.value.type = error.slotType
          registrationErrorMessages.value.push('Zum ausgewählten Zeitpunkt sind leider nicht mehr genügend Plätze frei.')
          break
      }
    }
  }
  else {
    registrationErrorMessages.value = [ `Beim Speichern der Registrierung ist ein Fehler aufgetreten. Bitte versuchen sie es erneut oder kontaktieren sie uns unter <a href="mailto:office@htlvb.at?subject=${props.event.title}" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a>.` ]
  }
}
</script>

<template>
  <div v-if="!hasFreeSlot && !isRegistered && !isRegistering && registrationErrorMessages.length === 0" class="flex justify-center items-center gap-2 p-4 rounded font-semibold">
    <span>
      Leider sind keine Termine mehr frei.
      Bitte kontaktieren sie uns unter
      <a :href="`mailto:office@htlvb.at?subject=${props.event.title}`" class="underline">office@htlvb.at</a> bzw.
      <a href="tel:+43767224605" class="underline">07672/24605</a>.
    </span>
  </div>
  <template v-else>
    <div v-html="infoText" class="my-6"></div>
    <form @submit.prevent="doRegister" class="my-6">
      <fieldset :disabled="isRegistering">
        <SlotSelection :slots="event.slots" v-model="selectedSlot" />
        <template v-if="selectedSlotMaxQuantity > 1">
          <h2 class="text-lg mt-4">Anzahl Personen</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <button v-for="quantity in _.range(1, selectedSlotMaxQuantity + 1)" :key="quantity" type="button" class="button"
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
          <template v-if="isRegistered && isConfirmationMailSent !== undefined">
            <span v-if="isConfirmationMailSent" class="text-green-500">
              Ihre Registrierung wurde erfolgreich gespeichert. Sie erhalten in Kürze eine Bestätigung per Mail.
            </span>
            <span v-else class="text-yellow-500">
              Ihre Registrierung wurde erfolgreich gespeichert.<br />
              Eine Bestätigungsmail konnte aber aufgrund eines internen Fehlers nicht versendet werden.<br />
              Sie können sich ihre Registrierung unter <a :href="`mailto:office@htlvb.at?subject=${props.event.title} - Reservierungsbestätigung`" class="underline">office@htlvb.at</a> bzw. <a href="tel:+43767224605" class="underline">07672/24605</a> bestätigen lassen.
            </span>
          </template>
          <ul v-else-if="registrationErrorMessages.length > 0" class="text-red-500">
            <li v-for="content in registrationErrorMessages" :key="content" v-html="content"></li>
          </ul>
        </div>
      </fieldset>
    </form>
  </template>
</template>